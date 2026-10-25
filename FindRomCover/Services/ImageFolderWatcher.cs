using System.Collections.Concurrent;

namespace FindRomCover.Services;

/// <summary>
///     Watches an image folder and, when a new or renamed image appears, optionally verifies
///     it, renames it to the pending target name and converts it to PNG.
/// </summary>
public sealed class ImageFolderWatcher : IDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".jpe", ".bmp", ".gif", ".webp", ".tiff", ".tif",
        ".avif", ".heic", ".heif", ".ico", ".svg", ".jxl", ".jp2"
    };

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    // Recently seen files mapped to their expiry ticks (UtcNow + 60 s). A delayed
    // removal only deletes the entry when no newer registration refreshed it past
    // the remover's stamp, so refreshing an entry extends its window instead of
    // letting the first timer expire it early.
    private readonly ConcurrentDictionary<string, long> _recentlyProcessed = new(AppConstants.FilePathComparer);
    private readonly Lock _renameLock = new();
    private readonly Lock _restartLock = new();
    private volatile bool _disposed;

    private string? _pendingRenameTarget;
    private FileSystemWatcher? _watcher;
    private string? _watchedFolderPath;
    private int _consecutiveErrorCount;
    private DateTime _lastErrorUtc = DateTime.MinValue;
    private bool _gaveUp;
    private long _watchGeneration;

    private const int MaxConsecutiveRestarts = 5;
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProcessedEntryLifetime = TimeSpan.FromSeconds(60);

    // A burst of image arrivals (copying or extracting a cover pack) must never be
    // renamed to the selected ROM one file at a time.
    private static readonly TimeSpan BulkImportWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BulkImportCooldown = TimeSpan.FromSeconds(15);
    private const int BulkImportEventThreshold = 3;
    private const int MaxFileReadyRetries = 3;
    private static readonly TimeSpan FileReadyRetryDelay = TimeSpan.FromSeconds(10);
    private readonly BulkImportDetector _bulkImportDetector =
        new(BulkImportEventThreshold, BulkImportWindow, BulkImportCooldown);
    private readonly Lock _bulkImportStateLock = new();
    private bool _bulkImportActive;
    private IReadOnlySet<string>? _knownRomNames;
    private int _pendingImageEvents;

    /// <summary>
    ///     Gets or sets the file name (without extension) that the next dropped image should be
    ///     renamed to.
    /// </summary>
    public string? PendingRenameTarget
    {
        get
        {
            lock (_renameLock)
            {
                return _pendingRenameTarget;
            }
        }
        set
        {
            lock (_renameLock)
            {
                var old = _pendingRenameTarget;
                _pendingRenameTarget = value;
                if (!string.Equals(old, value, StringComparison.OrdinalIgnoreCase))
                    LogService.Debug($"ImageFolderWatcher: PendingRenameTarget changed from '{old}' to '{value}'");
            }
        }
    }

    /// <summary>
    ///     Gets or sets the ROM names (without extension) found by the last scan. A new image
    ///     whose base name already matches one of these names is already a correctly named
    ///     cover and must not be renamed to the currently selected ROM.
    /// </summary>
    public IReadOnlySet<string>? KnownRomNames
    {
        get => Volatile.Read(ref _knownRomNames);
        set => Volatile.Write(ref _knownRomNames, value);
    }

    /// <summary>Gets the path of the folder currently being watched, or null when watching is stopped.</summary>
    public string? WatchedFolderPath
    {
        get
        {
            lock (_restartLock)
            {
                return _watchedFolderPath;
            }
        }
    }

    /// <summary>
    ///     True while the watcher is active and has not given up on automatic restarts.
    ///     After a give-up the underlying watcher is stopped so callers can detect the
    ///     dead state and revive it (e.g. by re-selecting the folder).
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_restartLock)
            {
                return _watcher != null && !_gaveUp;
            }
        }
    }

    /// <summary>Stops watching and releases all resources held by the watcher.</summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        try
        {
            _disposeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error cancelling dispose token: {ex.Message}");
        }

        StopCore(clearWatchedPath: true);

        try
        {
            _disposeCts.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error disposing CTS: {ex.Message}");
        }

        try
        {
            _processingLock.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error disposing processing lock: {ex.Message}");
        }
    }

    /// <summary>Raised with the image name (without extension) when a dropped image is ready.</summary>
    public event Action<string>? ImageFound;

    /// <summary>Raised with the file path and error message when PNG conversion fails.</summary>
    public event Action<string, string>? ConversionFailed;

    /// <summary>Raised with the file path and target name when AI verification rejects an image.</summary>
    public event Action<string, string>? VerificationFailed;

    /// <summary>
    ///     Gets or sets an optional AI verification callback that approves or rejects a file
    ///     before it is renamed.
    /// </summary>
    public Func<string, string, CancellationToken, Task<bool>>? VerifyImageAsync { get; set; }

    private void TryClearPendingRenameTarget(string? expectedValue)
    {
        lock (_renameLock)
        {
            if (!string.Equals(_pendingRenameTarget, expectedValue, StringComparison.OrdinalIgnoreCase)) return;

            var old = _pendingRenameTarget;
            _pendingRenameTarget = null;
            LogService.Debug($"ImageFolderWatcher: PendingRenameTarget cleared from '{old}'");
        }
    }

    /// <summary>
    ///     Registers a file path that the application is about to write so the watcher ignores
    ///     the resulting file event for the next minute.
    /// </summary>
    /// <param name="filePath">The file path expected to be created or renamed by the application.</param>
    public void PreRegisterExpectedFile(string filePath)
    {
        if (_disposed) return;

        var stamp = DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks;
        _recentlyProcessed.AddOrUpdate(filePath, stamp, (_, _) => stamp);
        CancellationToken token;
        try
        {
            token = _disposeCts.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ProcessedEntryLifetime, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            RemoveProcessedEntryIfExpired(filePath, stamp);
        });
        LogService.Debug($"ImageFolderWatcher: pre-registered '{Path.GetFileName(filePath)}' so watcher will skip it");
    }

    /// <summary>
    ///     Removes a previously pre-registered file path so the watcher processes its events again.
    /// </summary>
    /// <param name="filePath">The file path to unregister.</param>
    public void UnregisterExpectedFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (_recentlyProcessed.TryRemove(filePath, out _))
            LogService.Debug(
                $"ImageFolderWatcher: unregistered '{Path.GetFileName(filePath)}' after a failed save");
    }

    /// <summary>
    ///     Starts watching the specified folder for new and renamed image files.
    /// </summary>
    /// <param name="folderPath">The folder to watch.</param>
    /// <param name="resetErrorCount">
    ///     True for an explicit user start (reset the restart backoff); false for an
    ///     automatic restart so a flapping watcher eventually gives up.
    /// </param>
    /// <returns>True when watching started; false when the folder is missing or cannot be watched.</returns>
    public bool Start(string folderPath, bool resetErrorCount = true)
    {
        return StartCore(folderPath, resetErrorCount, expectedGeneration: null);
    }

    private bool StartCore(string folderPath, bool resetErrorCount, long? expectedGeneration)
    {
        if (_disposed) return false;

        long generation;
        lock (_restartLock)
        {
            // An automatic restart must not overwrite a watcher that the user started
            // after the restart was scheduled.
            if (expectedGeneration.HasValue && _watchGeneration != expectedGeneration.Value)
                return false;

            generation = ++_watchGeneration;
        }

        StopCore(clearWatchedPath: true);

        if (!Directory.Exists(folderPath))
        {
            LogService.WarningTransient($"ImageFolderWatcher: folder does not exist: {folderPath}");
            return false;
        }

        try
        {
            var watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.*",
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = false
            };

            // Subscribe before enabling events so no file event can be raised
            // before its handler is attached.
            watcher.Created += OnFileCreatedAsync;
            watcher.Renamed += OnFileRenamedAsync;
            watcher.Error += OnWatcherError;

            lock (_restartLock)
            {
                // A newer Start/Stop (for example the user selecting another folder)
                // superseded this call — do not replace its watcher.
                if (_disposed || generation != _watchGeneration)
                {
                    watcher.Dispose();
                    return false;
                }

                _watcher = watcher;
                _watchedFolderPath = folderPath;
                if (resetErrorCount) _consecutiveErrorCount = 0;
                _gaveUp = false;
            }

            try
            {
                watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Interlocked.CompareExchange(ref _watcher, null, watcher);
                watcher.Dispose();
                throw;
            }

            LogService.Information($"ImageFolderWatcher: started watching '{folderPath}'");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or IOException
                                       or ArgumentException
                                       or System.ComponentModel.Win32Exception
                                       or PlatformNotSupportedException
                                       or ObjectDisposedException)
        {
            // Environmental failure (ACLs, disconnected drive, invalid path, AV lock, ...).
            // Log as transient so it stays in the log file but does NOT trigger an
            // automatic bug report.
            LogService.WarningTransient(ex,
                $"ImageFolderWatcher: cannot watch folder '{folderPath}' — automatic detection disabled");
            return false;
        }
    }

    /// <summary>Stops watching the current folder.</summary>
    public void Stop()
    {
        // Invalidate any Start that is still in flight so a stopped watcher cannot
        // be installed afterwards.
        lock (_restartLock)
        {
            _watchGeneration++;
        }

        StopCore(clearWatchedPath: true);
    }

    /// <summary>
    ///     Stops the current watcher only when no newer Start/Stop superseded the caller.
    ///     The check and the stop run under the restart lock so a concurrent user Start
    ///     cannot be disposed by a stale automatic restart.
    /// </summary>
    private bool TryStopForRestart(long expectedGeneration)
    {
        lock (_restartLock)
        {
            if (_disposed || _watchGeneration != expectedGeneration) return false;

            StopCore(clearWatchedPath: false);
            return true;
        }
    }

    private void StopCore(bool clearWatchedPath)
    {
        if (clearWatchedPath)
            lock (_restartLock)
            {
                _watchedFolderPath = null;
            }

        var watcher = Interlocked.Exchange(ref _watcher, null);

        if (watcher == null) return;

        try
        {
            watcher.EnableRaisingEvents = false;
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error disabling events during stop: {ex.Message}");
        }

        try
        {
            watcher.Created -= OnFileCreatedAsync;
            watcher.Renamed -= OnFileRenamedAsync;
            watcher.Error -= OnWatcherError;
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error unsubscribing events during stop: {ex.Message}");
        }

        try
        {
            watcher.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error disposing watcher: {ex.Message}");
        }

        // Wait for any in-flight ProcessFileAsync to complete without blocking the
        // caller (often the UI thread): an AI verification can hold the lock for seconds.
        _ = Task.Run(WaitForProcessingAsync);

        LogService.Information("ImageFolderWatcher: stopped");
    }

    private async Task WaitForProcessingAsync()
    {
        try
        {
            if (!await _processingLock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
                LogService.WarningTransient("ImageFolderWatcher: timed out waiting for in-flight processing to complete");
            else
                _processingLock.Release();
        }
        catch (ObjectDisposedException)
        {
            // Dispose raced with Stop — nothing to wait for.
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: error waiting for in-flight processing: {ex.Message}");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // NOTE: This must NEVER call LogService.Error — FileSystemWatcher failures are
        // environmental (access denied, drive disconnected, buffer overflow, AV lock, ...)
        // and would otherwise spam the automatic bug-report pipeline (see issue #66867:
        // Win32Exception (5) "Accesso negato").
        var ex = e.GetException();

        try
        {
            if (ex is InternalBufferOverflowException overflowEx)
            {
                LogService.WarningTransient(overflowEx,
                    "ImageFolderWatcher: event buffer overflowed — some file events may have been lost");
                return;
            }

            LogService.WarningTransient(ex,
                $"ImageFolderWatcher: transient watcher error watching '{WatchedFolderPath}' — attempting recovery");

            ScheduleRestart();
        }
        catch (Exception handlerEx)
        {
            LogService.Debug($"ImageFolderWatcher: error in OnWatcherError handler: {handlerEx.Message}");
        }
    }

    private void ScheduleRestart()
    {
        string? folderPath;
        CancellationToken token;
        long generation;
        var gaveUp = false;
        lock (_restartLock)
        {
            if (_disposed) return;

            generation = _watchGeneration;

            var now = DateTime.UtcNow;
            if (now - _lastErrorUtc > RestartCooldown)
                _consecutiveErrorCount = 0;

            _lastErrorUtc = now;
            _consecutiveErrorCount++;

            if (_consecutiveErrorCount > MaxConsecutiveRestarts)
            {
                _gaveUp = true;
                gaveUp = true;
                LogService.WarningTransient(
                    $"ImageFolderWatcher: giving up automatic restart after {_consecutiveErrorCount - 1} attempts for '{_watchedFolderPath}' — automatic detection disabled until the folder is re-selected");
                folderPath = _watchedFolderPath;
            }
            else
            {
                folderPath = _watchedFolderPath;
            }
        }

        if (gaveUp)
        {
            // Drop the broken watcher so IsWatching reports false and a re-selection
            // of the same folder can revive it. Do not clear the path intent.
            StopCore(clearWatchedPath: false);
            return;
        }

        if (string.IsNullOrEmpty(folderPath)) return;

        try
        {
            token = _disposeCts.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RestartDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (_disposed) return;

            // Abort if the watched folder changed (user re-selected) or was explicitly stopped.
            lock (_restartLock)
            {
                if (_disposed) return;
                if (_watchGeneration != generation) return;
                if (!AppConstants.FilePathComparer.Equals(_watchedFolderPath, folderPath)) return;
            }

            if (!Directory.Exists(folderPath))
            {
                LogService.WarningTransient(
                    $"ImageFolderWatcher: watched folder no longer exists, not restarting: '{folderPath}'");

                // Drop the broken watcher so IsWatching reports false and a re-selection
                // can revive it, then retry a few times in case the drive is only briefly
                // unavailable (the normal backoff gives up after MaxConsecutiveRestarts).
                if (TryStopForRestart(generation)) ScheduleRestart();
                return;
            }

            try
            {
                // Drop the broken watcher without clearing the path intent, then re-start.
                // The generation check prevents a stale restart from replacing a watcher
                // the user started for a different folder in the meantime.
                if (!TryStopForRestart(generation)) return;
                if (_disposed) return;

                if (StartCore(folderPath, resetErrorCount: false, generation))
                    LogService.Information($"ImageFolderWatcher: recovered watching '{folderPath}'");
            }
            catch (Exception restartEx)
            {
                LogService.Debug($"ImageFolderWatcher: restart attempt failed: {restartEx.Message}");
            }
        });
    }

    private async void OnFileCreatedAsync(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (_disposed) return;

            LogService.Debug($"ImageFolderWatcher: OnFileCreated fired for '{e.FullPath}'");

            var counted = RecordExternalImageEvent(e.FullPath);
            try
            {
                await ProcessFileAsync(e.FullPath);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LogService.Error(ex, $"ImageFolderWatcher: unhandled error in OnFileCreated for '{e.FullPath}'");
            }
            finally
            {
                if (counted) CompleteImageEvent();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in method OnFileCreatedAsync");
        }
    }

    private async void OnFileRenamedAsync(object sender, RenamedEventArgs e)
    {
        try
        {
            if (_disposed) return;

            LogService.Debug($"ImageFolderWatcher: OnFileRenamed fired for '{e.FullPath}'");

            var counted = RecordExternalImageEvent(e.FullPath);
            try
            {
                await ProcessFileAsync(e.FullPath);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LogService.Error(ex, $"ImageFolderWatcher: unhandled error in OnFileRenamed for '{e.FullPath}'");
            }
            finally
            {
                if (counted) CompleteImageEvent();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in method OnFileRenamedAsync");
        }
    }

    private void ScheduleDedupeCleanup(string key)
    {
        CancellationToken token;
        try
        {
            if (_disposed) return;
            token = _disposeCts.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var stamp = DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ProcessedEntryLifetime, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            RemoveProcessedEntryIfExpired(key, stamp);
        });
    }

    private void RemoveProcessedEntryIfExpired(string key, long stamp)
    {
        // Delete only when the entry was not refreshed past our stamp; a newer
        // registration (or a re-added processing entry) keeps the entry alive.
        if (_recentlyProcessed.TryGetValue(key, out var current) && current <= stamp)
            _recentlyProcessed.TryRemove(KeyValuePair.Create(key, current));
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    /// <summary>
    ///     Records an external image event for bulk-import detection. Files the application
    ///     is writing itself are pre-registered and must not count towards a burst.
    /// </summary>
    /// <returns><see langword="true" /> when the caller must call <see cref="CompleteImageEvent" />.</returns>
    private bool RecordExternalImageEvent(string filePath)
    {
        if (_recentlyProcessed.ContainsKey(filePath)) return false;
        if (!IsSupportedImageFile(filePath)) return false;

        var isBulk = _bulkImportDetector.Record(DateTime.UtcNow);
        lock (_bulkImportStateLock)
        {
            _pendingImageEvents++;
            if (isBulk) _bulkImportActive = true;
        }

        return true;
    }

    /// <summary>
    ///     Marks one recorded image event as processed. The bulk-import state stays active
    ///     until every event that arrived in the burst has been handled, so a long queue of
    ///     files cannot start being renamed after a fixed cooldown.
    /// </summary>
    private void CompleteImageEvent()
    {
        lock (_bulkImportStateLock)
        {
            if (_pendingImageEvents > 0) _pendingImageEvents--;
            if (_pendingImageEvents == 0) _bulkImportActive = false;
        }
    }

    private bool IsBulkImportActive()
    {
        lock (_bulkImportStateLock)
        {
            return _bulkImportActive;
        }
    }

    private bool IsKnownRomName(string fileNameWithoutExtension)
    {
        var knownRomNames = Volatile.Read(ref _knownRomNames);
        return knownRomNames != null && knownRomNames.Contains(fileNameWithoutExtension);
    }

    private async Task RetryProcessFileAsync(string filePath, int attempt)
    {
        try
        {
            await Task.Delay(FileReadyRetryDelay);
            if (_disposed || !File.Exists(filePath)) return;

            await ProcessFileAsync(filePath, attempt);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogService.Debug($"ImageFolderWatcher: retry for '{filePath}' failed: {ex.Message}");
        }
    }

    private async Task ProcessFileAsync(string filePath, int attempt = 0)
    {
        try
        {
            if (_disposed) return;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!SupportedExtensions.Contains(extension))
            {
                LogService.Debug(
                    $"ImageFolderWatcher: skipping '{Path.GetFileName(filePath)}' — extension '{extension}' not supported");
                return;
            }

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            if (string.IsNullOrEmpty(fileNameWithoutExt))
                return;

            try
            {
                await _processingLock.WaitAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var lockAcquired = true;
            try
            {
                // Deduplicate INSIDE the lock to prevent races with file system events
                // that fire during our own rename/convert operations
                if (!_recentlyProcessed.TryAdd(filePath, DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks))
                {
                    LogService.Debug($"ImageFolderWatcher: skipping '{Path.GetFileName(filePath)}' — dedup hit");
                    return;
                }

                // Clean up old dedupe entries after 60 seconds (must outlast any in-lock wait)
                ScheduleDedupeCleanup(filePath);

                if (!await WaitForFileReadyAsync(filePath))
                {
                    _recentlyProcessed.TryRemove(filePath, out _);

                    // Bulk imports and already-correctly-named covers are left alone; a
                    // retry would only risk renaming a file that is still being copied.
                    if (IsBulkImportActive() || IsKnownRomName(fileNameWithoutExt))
                    {
                        LogService.Debug(
                            $"ImageFolderWatcher: '{Path.GetFileName(filePath)}' was not stable — leaving it untouched");
                        return;
                    }

                    if (attempt < MaxFileReadyRetries)
                    {
                        LogService.WarningTransient(
                            $"ImageFolderWatcher: '{Path.GetFileName(filePath)}' is not stable yet — retrying ({attempt + 1}/{MaxFileReadyRetries})");
                        _ = RetryProcessFileAsync(filePath, attempt + 1);
                        return;
                    }

                    LogService.WarningTransient(
                        $"ImageFolderWatcher: '{Path.GetFileName(filePath)}' is not accessible — skipping processing");
                    return;
                }

                if (!File.Exists(filePath))
                {
                    _recentlyProcessed.TryRemove(filePath, out _);

                    var pngEquivalent = Path.Combine(
                        Path.GetDirectoryName(filePath) ?? ".",
                        Path.GetFileNameWithoutExtension(filePath) + ".png");

                    if (File.Exists(pngEquivalent))
                        LogService.Debug(
                            $"ImageFolderWatcher: source file gone after PNG conversion: '{Path.GetFileName(filePath)}'");
                    else
                        LogService.Debug($"ImageFolderWatcher: file disappeared after wait: '{filePath}'");

                    return;
                }

                var renameTarget = PendingRenameTarget;
                if (IsBulkImportActive())
                {
                    _recentlyProcessed.TryRemove(filePath, out _);
                    LogService.Debug(
                        $"ImageFolderWatcher: bulk import in progress — leaving '{Path.GetFileName(filePath)}' untouched");
                    return;
                }

                if (!string.IsNullOrEmpty(renameTarget) &&
                    IsKnownRomName(fileNameWithoutExt) &&
                    !string.Equals(fileNameWithoutExt, renameTarget, StringComparison.OrdinalIgnoreCase))
                {
                    _recentlyProcessed.TryRemove(filePath, out _);
                    LogService.Debug(
                        $"ImageFolderWatcher: '{Path.GetFileName(filePath)}' already matches a ROM name — leaving it untouched");
                    return;
                }

                LogService.Debug(
                    $"ImageFolderWatcher: processing '{Path.GetFileName(filePath)}' — PendingRenameTarget = '{renameTarget}'");

                if (!string.IsNullOrEmpty(renameTarget) && VerifyImageAsync != null)
                {
                    var verified = true;
                    try
                    {
                        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        verified = await VerifyImageAsync(filePath, renameTarget, verifyCts.Token);
                    }
                    catch (Exception ex)
                    {
                        LogService.ErrorWatcher(ex,
                            $"ImageFolderWatcher: AI verification failed for '{filePath}' — proceeding with rename");
                    }

                    if (!verified)
                    {
                        _recentlyProcessed.TryRemove(filePath, out _);
                        LogService.WarningTransient(
                            $"ImageFolderWatcher: AI verification rejected '{Path.GetFileName(filePath)}' for '{renameTarget}' — rename skipped");
                        VerificationFailed?.Invoke(filePath, renameTarget);
                        return;
                    }
                }

                var wasRenamed = false;
                if (!string.IsNullOrEmpty(renameTarget))
                {
                    var directory = Path.GetDirectoryName(filePath);
                    var renamedPath = Path.Combine(directory ?? ".", renameTarget + extension);

                    // macOS defaults to a case-insensitive file system, so "game.png" and
                    // "Game.png" can be the same file even though FilePathComparer is
                    // ordinal there. Treating it as a different file produced "Game (1).png".
                    var sameFile = AppConstants.FilePathComparer.Equals(filePath, renamedPath);
                    if (!sameFile && OperatingSystem.IsMacOS())
                        sameFile = string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(renamedPath),
                            StringComparison.OrdinalIgnoreCase);

                    if (sameFile)
                    {
                        LogService.Debug(
                            $"ImageFolderWatcher: source and target are the same ('{Path.GetFileName(filePath)}'), skipping rename");
                        wasRenamed = true;
                        TryClearPendingRenameTarget(renameTarget);
                    }
                    else
                    {
                        if (File.Exists(renamedPath))
                        {
                            // Never delete an existing (possibly good) cover. Rename the
                            // dropped file to a free name instead.
                            LogService.Debug(
                                $"ImageFolderWatcher: target '{Path.GetFileName(renamedPath)}' already exists — renaming to a free name instead of deleting it");
                            renamedPath = GetFreeRenameTargetPath(renamedPath);
                        }

                        // Register the target before the move so the watcher's own rename
                        // event is not mistaken for an external file or a bulk import.
                        PreRegisterExpectedFile(renamedPath);
                        var renamed = await MoveFileWithRetryAsync(filePath, renamedPath);
                        if (renamed)
                        {
                            wasRenamed = true;
                            TryClearPendingRenameTarget(renameTarget);
                            _recentlyProcessed.TryRemove(filePath, out _);
                            _recentlyProcessed.AddOrUpdate(renamedPath,
                                DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks,
                                (_, _) => DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks);
                            ScheduleDedupeCleanup(renamedPath);
                            LogService.Debug(
                                $"ImageFolderWatcher: renamed '{Path.GetFileName(filePath)}' to '{Path.GetFileName(renamedPath)}'");
                            filePath = renamedPath;
                        }
                        else
                        {
                            UnregisterExpectedFile(renamedPath);
                            LogService.WarningTransient(
                                $"ImageFolderWatcher: failed to rename '{filePath}' to '{renamedPath}' after retries — PendingRenameTarget kept for next file");
                        }
                    }
                }
                else
                {
                    LogService.Debug(
                        $"ImageFolderWatcher: no PendingRenameTarget set for '{Path.GetFileName(filePath)}' — skipping rename");
                }

                if (!wasRenamed)
                {
                    _recentlyProcessed.TryRemove(filePath, out _);
                    LogService.Debug(
                        $"ImageFolderWatcher: skipping conversion for '{Path.GetFileName(filePath)}' — file was not renamed to a game name");
                    return;
                }

                var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
                var needsConversion = !isPng || !ImageProcessor.IsValidPng(filePath);

                if (needsConversion)
                {
                    string? expectedPngPath = null;
                    if (!isPng)
                    {
                        // Never overwrite an existing cover with the converted PNG: the
                        // dropped file is kept under its current name instead.
                        expectedPngPath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".",
                            Path.GetFileNameWithoutExtension(filePath) + ".png");
                        if (File.Exists(expectedPngPath))
                        {
                            _recentlyProcessed.TryRemove(filePath, out _);
                            LogService.WarningTransient(
                                $"ImageFolderWatcher: '{Path.GetFileName(expectedPngPath)}' already exists — '{Path.GetFileName(filePath)}' was kept as-is to protect the existing cover");
                            ConversionFailed?.Invoke(filePath,
                                $"A cover named '{Path.GetFileName(expectedPngPath)}' already exists.\n\nThe dropped file was kept unchanged.");
                            return;
                        }

                        // Register the conversion target before it is written so the
                        // watcher's own output does not look like an external arrival.
                        PreRegisterExpectedFile(expectedPngPath);
                    }

                    var (convertedPath, convertError) = await ConvertToPngWithRetryAsync(filePath);
                    if (convertedPath == null)
                    {
                        if (expectedPngPath != null) UnregisterExpectedFile(expectedPngPath);
                        LogService.Debug(
                            $"ImageFolderWatcher: conversion to PNG failed for '{filePath}' — ImageFound NOT fired");
                        ConversionFailed?.Invoke(filePath, convertError ?? "Unknown error");
                        return;
                    }

                    _recentlyProcessed.AddOrUpdate(convertedPath,
                        DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks,
                        (_, _) => DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks);
                    ScheduleDedupeCleanup(convertedPath);

                    filePath = convertedPath;
                }

                fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                LogService.Debug($"ImageFolderWatcher: firing ImageFound for '{fileNameWithoutExt}'");
                ImageFound?.Invoke(fileNameWithoutExt);
            }
            catch (Exception ex)
            {
                LogService.ErrorWatcher(ex, $"Error processing new image in folder: {filePath}");
            }
            finally
            {
                if (lockAcquired)
                    try
                    {
                        _processingLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SemaphoreFullException)
                    {
                    }
            }
        }
        catch (ObjectDisposedException)
        {
            // Shutting down — not a bug.
            LogService.Debug("ImageFolderWatcher: processing aborted during shutdown");
        }
        catch (Exception ex)
        {
            LogService.ErrorWatcher(ex, "Error processing new image in folder.");
        }
    }

    private static async Task<bool> WaitForFileReadyAsync(string filePath)
    {
        const int maxWaitMs = 20000;
        const int pollIntervalMs = 250;
        const int stableChecksRequired = 4;
        var elapsed = 0;
        long lastSize = -1;
        var stableCount = 0;

        while (elapsed < maxWaitMs)
        {
            // A duplicate/late event can reference a path that was already renamed away.
            // Without this check File.Open throws FileNotFoundException (an IOException)
            // and the loop would hold the single processing slot for the full 20 s.
            if (!File.Exists(filePath)) return false;

            try
            {
                // Keep the sharing mode permissive so the browser can finish writing;
                // readiness is determined by a stable file size (1 s by default).
                await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 0)
                {
                    if (stream.Length == lastSize)
                    {
                        stableCount++;
                        if (stableCount >= stableChecksRequired)
                            return true;
                    }
                    else
                    {
                        stableCount = 0;
                        lastSize = stream.Length;
                    }
                }
                else
                {
                    stableCount = 0;
                    lastSize = 0;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                stableCount = 0;
                lastSize = -1;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;
        }

        // Timed out: the file was never stable long enough to be processed safely.
        // Returning true here would rename/convert a partially written file and, for
        // non-PNG sources, delete the file the writer is still filling.
        LogService.Debug(
            $"ImageFolderWatcher: file '{Path.GetFileName(filePath)}' was not stable after {maxWaitMs} ms (last size: {lastSize}).");
        return false;
    }

    private static string GetFreeRenameTargetPath(string preferredPath)
    {
        if (!File.Exists(preferredPath)) return preferredPath;

        var directory = Path.GetDirectoryName(preferredPath) ?? ".";
        var fileName = Path.GetFileNameWithoutExtension(preferredPath);
        var extension = Path.GetExtension(preferredPath);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{fileName} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return preferredPath;
    }

    private static async Task<bool> MoveFileWithRetryAsync(string sourcePath, string targetPath)
    {
        const int maxRetries = 5;
        const int baseDelayMs = 200;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
            try
            {
                File.Move(sourcePath, targetPath);
                return true;
            }
            catch (FileNotFoundException)
            {
                LogService.Debug(
                    $"MoveFileWithRetryAsync: source file not found (may have been deleted): '{sourcePath}'");
                return false;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                await Task.Delay((int)delay);
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                await Task.Delay((int)delay);
            }
            catch (Exception ex)
            {
                LogService.ErrorWatcher(ex,
                    $"MoveFileWithRetryAsync: attempt {attempt} failed for '{sourcePath}' -> '{targetPath}'");
                if (attempt >= maxRetries) return false;

                var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                await Task.Delay((int)delay);
            }

        return false;
    }

    private static async Task<(string? Path, string? Error)> ConvertToPngWithRetryAsync(string sourcePath)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 300;
        string? lastError = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            var (convertedPath, error) = await ConvertToPngAsync(sourcePath);
            if (convertedPath != null) return (convertedPath, null);

            lastError = error;
            if (attempt < maxRetries)
            {
                LogService.Debug($"ImageFolderWatcher: convert retry {attempt}/{maxRetries} for '{sourcePath}'");
                var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                await Task.Delay((int)delay);
            }
        }

        // Conversion failures are environmental (partial browser download, unsupported
        // format, locked file): keep them out of automatic bug reports.
        LogService.ErrorWatcher(
            new InvalidOperationException(lastError ?? "Unknown image conversion error"),
            $"ImageFolderWatcher: failed to convert '{sourcePath}' to PNG after {maxRetries} attempts.");

        return (null, lastError);
    }

    /// <summary>
    ///     Converts a dropped image to PNG through the shared tolerant image processor.
    ///     A valid PNG is left as-is; an invalid file named <c>.png</c> is normalized in
    ///     place, while other formats are written next to the source and the original is
    ///     deleted after a successful conversion.
    /// </summary>
    /// <param name="sourcePath">The dropped image path.</param>
    /// <returns>The final PNG path and an error message when conversion fails.</returns>
    internal static async Task<(string? Path, string? Error)> ConvertToPngAsync(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? ".";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var isPng = Path.GetExtension(sourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase);

        if (isPng && ImageProcessor.IsValidPng(sourcePath))
            return (sourcePath, null);

        // A valid .png is left untouched by the caller. An invalid .png (browser saved
        // WebP/JPEG bytes or a truncated PNG under a .png name) is normalized in place
        // through a temporary file; other formats are converted next to the source.
        var targetPath = isPng
            ? Path.Combine(directory, $"{fileNameWithoutExt}.{Guid.NewGuid():N}.tmp")
            : Path.Combine(directory, fileNameWithoutExt + ".png");

        // Use the shared ImageProcessor path so the watcher gets the same tolerant
        // fallback as manual saves: content sniffing for lying extensions, PNG CRC
        // tolerance, atomic temp writes and retry handling (issues #68280, #68282).
        var result = await ImageProcessor.ConvertAndSaveImageAsync(sourcePath, targetPath, CancellationToken.None);
        if (!result.Success)
        {
            LogService.Debug($"ImageFolderWatcher: conversion failed for '{sourcePath}': {result.ErrorMessage}");
            TryDeleteFile(targetPath);
            return (null, result.ErrorMessage);
        }

        if (isPng)
            try
            {
                File.Move(targetPath, sourcePath, true);
                return (sourcePath, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDeleteFile(targetPath);
                return (null, $"Could not replace the invalid PNG file: {ex.Message}");
            }

        TryDeleteFile(sourcePath);
        return (targetPath, null);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogService.WarningTransient(ex, $"ImageFolderWatcher: failed to delete file '{path}'.");
        }
    }
}