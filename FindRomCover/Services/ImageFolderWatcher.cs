using System.Collections.Concurrent;
using System.IO;
using ImageMagick;

namespace FindRomCover.Services;

public sealed class ImageFolderWatcher : IDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif",
        ".avif", ".heic", ".heif", ".ico", ".svg", ".jxl", ".jp2"
    };

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _recentlyProcessed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _renameLock = new();
    private readonly Lock _restartLock = new();
    private volatile bool _disposed;

    private string? _pendingRenameTarget;
    private FileSystemWatcher? _watcher;
    private string? _watchedFolderPath;
    private int _consecutiveErrorCount;
    private DateTime _lastErrorUtc = DateTime.MinValue;
    private bool _gaveUp;

    private const int MaxConsecutiveRestarts = 5;
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(2);

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

    public event Action<string>? ImageFound;
    public event Action<string, string>? ConversionFailed;
    public event Action<string, string>? VerificationFailed;

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

    public void PreRegisterExpectedFile(string filePath)
    {
        if (_disposed) return;

        _recentlyProcessed.TryAdd(filePath, 1);
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
                await Task.Delay(60000, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _recentlyProcessed.TryRemove(filePath, out _);
        });
        LogService.Debug($"ImageFolderWatcher: pre-registered '{Path.GetFileName(filePath)}' so watcher will skip it");
    }

    public bool Start(string folderPath)
    {
        if (_disposed) return false;

        StopCore(clearWatchedPath: true);

        if (!Directory.Exists(folderPath))
        {
            LogService.Warning($"ImageFolderWatcher: folder does not exist: {folderPath}");
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
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreatedAsync;
            watcher.Renamed += OnFileRenamedAsync;
            watcher.Error += OnWatcherError;

            _watcher = watcher;
            lock (_restartLock)
            {
                _watchedFolderPath = folderPath;
                _consecutiveErrorCount = 0;
                _gaveUp = false;
            }

            LogService.Information($"ImageFolderWatcher: started watching '{folderPath}'");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or IOException
                                       or ArgumentException
                                       or System.ComponentModel.Win32Exception
                                       or PlatformNotSupportedException)
        {
            // Environmental failure (ACLs, disconnected drive, invalid path, AV lock, ...).
            // Log as Warning so it does NOT trigger an automatic bug report.
            LogService.Warning(ex, $"ImageFolderWatcher: cannot watch folder '{folderPath}' — automatic detection disabled");
            return false;
        }
    }

    public void Stop()
    {
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

        // Wait for any in-flight ProcessFileAsync to complete.
        // Do not hold any lock while waiting to avoid deadlocks with OnWatcherError.
        try
        {
            if (!_processingLock.Wait(TimeSpan.FromSeconds(15)))
                LogService.Warning("ImageFolderWatcher: timed out waiting for in-flight processing to complete");
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

        LogService.Information("ImageFolderWatcher: stopped");
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
                LogService.Warning(overflowEx,
                    "ImageFolderWatcher: event buffer overflowed — some file events may have been lost");
                return;
            }

            LogService.Warning(ex,
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
                LogService.Warning(
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
                LogService.Warning(
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

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(60000, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _recentlyProcessed.TryRemove(key, out _);
        });
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
                if (!_recentlyProcessed.TryAdd(filePath, 1))
                {
                    LogService.Debug($"ImageFolderWatcher: skipping '{Path.GetFileName(filePath)}' — dedup hit");
                    return;
                }

                // Clean up old dedupe entries after 60 seconds (must outlast any in-lock wait)
                ScheduleDedupeCleanup(filePath);

                if (!await WaitForFileReadyAsync(filePath))
                {
                    LogService.Warning(
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
                        LogService.Warning(ex,
                            $"ImageFolderWatcher: AI verification failed for '{filePath}' — proceeding with rename");
                    }

                    if (!verified)
                    {
                        _recentlyProcessed.TryRemove(filePath, out _);
                        LogService.Warning(
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
                            _recentlyProcessed.TryAdd(renamedPath, 1);
                            ScheduleDedupeCleanup(renamedPath);
                            LogService.Debug(
                                $"ImageFolderWatcher: renamed '{Path.GetFileName(filePath)}' to '{Path.GetFileName(renamedPath)}'");
                            filePath = renamedPath;
                        }
                        else
                        {
                            LogService.Warning(
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
                    var (convertedPath, convertError) = await ConvertToPngWithRetryAsync(filePath);
                    if (convertedPath == null)
                    {
                        LogService.Debug(
                            $"ImageFolderWatcher: conversion to PNG failed for '{filePath}' — ImageFound NOT fired");
                        ConversionFailed?.Invoke(filePath, convertError ?? "Unknown error");
                        return;
                    }

                    _recentlyProcessed.TryAdd(convertedPath, 1);
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

        return true;
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
                LogService.Warning(ex,
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

        var settings = ImageProcessor.GetMagickReadSettings(sourcePath);
        using var magickImage = new MagickImage(sourcePath, settings);
        magickImage.AutoOrient();
        magickImage.Quality = 90;
        magickImage.Format = MagickFormat.Png;

        await magickImage.WriteAsync(targetPath);

        if (File.Exists(sourcePath) && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            try
            {
                File.Delete(sourcePath);
            }
            catch (Exception ex)
            {
                LogService.Warning(ex,
                    $"ImageFolderWatcher: failed to delete source file after PNG conversion: '{sourcePath}'");
            }

        return (targetPath, null);
    }
}