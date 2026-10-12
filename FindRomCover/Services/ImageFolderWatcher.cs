using System.Collections.Concurrent;
using ImageMagick;

namespace FindRomCover.Services;

/// <summary>
///     Watches an image folder and, when a new or renamed image appears, optionally verifies
///     it, renames it to the pending target name and converts it to PNG.
/// </summary>
public sealed class ImageFolderWatcher : IDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif",
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
    /// <returns>True when watching started; false when the folder is missing or cannot be watched.</returns>
    public bool Start(string folderPath)
    {
        if (_disposed) return false;

        long generation;
        lock (_restartLock)
        {
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
                _consecutiveErrorCount = 0;
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
        var gaveUp = false;
        lock (_restartLock)
        {
            if (_disposed) return;

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
                if (!string.Equals(_watchedFolderPath, folderPath, StringComparison.OrdinalIgnoreCase)) return;
            }

            if (!Directory.Exists(folderPath))
            {
                LogService.WarningTransient(
                    $"ImageFolderWatcher: watched folder no longer exists, not restarting: '{folderPath}'");
                return;
            }

            try
            {
                // Drop the broken watcher without clearing the path intent, then re-start.
                StopCore(clearWatchedPath: false);
                if (_disposed) return;

                if (Start(folderPath))
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

    private async Task ProcessFileAsync(string filePath)
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

                    if (string.Equals(filePath, renamedPath, StringComparison.OrdinalIgnoreCase))
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

                        var renamed = await MoveFileWithRetryAsync(filePath, renamedPath);
                        if (renamed)
                        {
                            wasRenamed = true;
                            TryClearPendingRenameTarget(renameTarget);
                            _recentlyProcessed.TryRemove(filePath, out _);
                            _recentlyProcessed.TryAdd(renamedPath, DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks);
                            ScheduleDedupeCleanup(renamedPath);
                            LogService.Debug(
                                $"ImageFolderWatcher: renamed '{Path.GetFileName(filePath)}' to '{Path.GetFileName(renamedPath)}'");
                            filePath = renamedPath;
                        }
                        else
                        {
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

                if (!Path.GetExtension(filePath).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Never overwrite an existing cover with the converted PNG: the
                    // dropped file is kept under its current name instead.
                    var pngTarget = Path.Combine(Path.GetDirectoryName(filePath) ?? ".",
                        Path.GetFileNameWithoutExtension(filePath) + ".png");
                    if (File.Exists(pngTarget))
                    {
                        _recentlyProcessed.TryRemove(filePath, out _);
                        LogService.WarningTransient(
                            $"ImageFolderWatcher: '{Path.GetFileName(pngTarget)}' already exists — '{Path.GetFileName(filePath)}' was kept as-is to protect the existing cover");
                        ConversionFailed?.Invoke(filePath,
                            $"A cover named '{Path.GetFileName(pngTarget)}' already exists.\n\nThe dropped file was kept unchanged.");
                        return;
                    }

                    var (convertedPath, convertError) = await ConvertToPngWithRetryAsync(filePath);
                    if (convertedPath == null)
                    {
                        LogService.Debug(
                            $"ImageFolderWatcher: conversion to PNG failed for '{filePath}' — ImageFound NOT fired");
                        ConversionFailed?.Invoke(filePath, convertError ?? "Unknown error");
                        return;
                    }

                    _recentlyProcessed.TryAdd(convertedPath, DateTime.UtcNow.Add(ProcessedEntryLifetime).Ticks);
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
        const int maxWaitMs = 10000;
        const int pollIntervalMs = 250;
        const int stableChecksRequired = 2;
        var elapsed = 0;
        long lastSize = -1;
        var stableCount = 0;

        while (elapsed < maxWaitMs)
        {
            try
            {
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

        // Timed out: only treat the file as ready when at least some content was
        // readable. Returning true for an empty/never-readable file sends a file that
        // is still being copied into conversion and produces spurious error popups.
        LogService.Debug(
            $"ImageFolderWatcher: file '{Path.GetFileName(filePath)}' was not stable after {maxWaitMs} ms (last size: {lastSize}).");
        return lastSize > 0;
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
            try
            {
                return await ConvertToPngAsync(sourcePath);
            }
            catch (MagickException ex)
            {
                LogService.ErrorWatcher(ex,
                    $"ImageFolderWatcher: image conversion skipped for corrupt/misformatted file: {sourcePath}");
                return (null, ex.Message);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                LogService.ErrorWatcher(ex, $"Failed to convert image to PNG: {sourcePath}");
                if (attempt < maxRetries)
                {
                    LogService.Debug($"ImageFolderWatcher: convert retry {attempt}/{maxRetries} for '{sourcePath}'");
                    var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                    await Task.Delay((int)delay);
                }
            }

        return (null, lastError);
    }

    private static async Task<(string? Path, string? Error)> ConvertToPngAsync(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var targetPath = Path.Combine(directory ?? ".", fileNameWithoutExt + ".png");
        var tempPath = Path.Combine(directory ?? ".", Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var settings = ImageProcessor.GetMagickReadSettings(sourcePath);
            using var magickImage = new MagickImage(sourcePath, settings);
            magickImage.AutoOrient();
            magickImage.Quality = 90;
            magickImage.Format = MagickFormat.Png;

            // Write to a temp file and move into place so a crash or locked target
            // never leaves a truncated cover behind.
            await magickImage.WriteAsync(tempPath);
            File.Move(tempPath, targetPath, true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, $"ImageFolderWatcher: failed to clean up temp file '{tempPath}'.");
            }
        }

        if (File.Exists(sourcePath) && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex,
                    $"ImageFolderWatcher: failed to delete source file after PNG conversion: '{sourcePath}'");
            }

        return (targetPath, null);
    }
}