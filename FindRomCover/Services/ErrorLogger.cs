using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services;

/// <summary>
///     Writes bug reports to the local user/API log files and forwards them to the
///     remote bug report API defined by <see cref="AppConstants.BugReportApiUrl" />.
/// </summary>
public static class ErrorLogger
{
    private const string ApiKey = AppConstants.BugReportApiKey;
    private const string BugReportApiUrl = AppConstants.BugReportApiUrl;

    /// <summary>
    ///     Default timeout, in seconds, for a bug-report API request.
    /// </summary>
    public const int DefaultApiTimeoutSeconds = 30;

    /// <summary>
    ///     Indicates whether the logger has been disposed. Exposed for tests.
    /// </summary>
    internal static volatile bool IsDisposed;

    /// <summary>
    ///     Guards <see cref="Dispose" /> and <see cref="IsDisposed" />. Exposed for tests.
    /// </summary>
    internal static readonly Lock DisposeLock = new();

    // The paths are resolved on every use instead of being cached in static fields:
    // a static field is initialized when the type is first touched, which can happen
    // before AppDataPaths.BaseDirectoryOverride is set (tests) and would then pin the
    // real %LocalAppData% folder for the rest of the process.

    /// <summary>Path of the log file holding reports not yet accepted by the API.</summary>
    internal static string ApiLogFilePath => AppDataPaths.LogFilePath("ApiLogError.txt");

    /// <summary>Path of the log file holding all user-facing error reports.</summary>
    private static string UserLogFilePath => AppDataPaths.LogFilePath("UserLogError.txt");

    /// <summary>Path of the log file holding internal diagnostics of the logger itself.</summary>
    private static string InternalLogFilePath => AppDataPaths.LogFilePath("InternalLog.txt");

    /// <summary>Path of the JSON queue used to retry reports that could not be sent.</summary>
    private static string PendingReportsFilePath => AppDataPaths.LogFilePath("PendingBugReports.json");

    private static readonly SemaphoreSlim LogFileLock = new(1, 1);
    private static readonly SemaphoreSlim PendingQueueLock = new(1, 1);
    private static readonly SemaphoreSlim InternalLogSemaphore = new(1, 1);
    // A named Semaphore (not Mutex) is safe to hold across await: semaphores are not
    // thread-affine, so the continuation may resume on a different thread pool thread.
    private static readonly Lazy<Semaphore> PendingQueueMutex = new(CreatePendingQueueMutex,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private const long MaxLogFileBytes = 4L * 1024 * 1024;
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    ///     Test seam that replaces the real HTTP send. Exposed for tests.
    /// </summary>
    internal static Func<BugReportModel, int, Task<bool>>? SendToApiOverride;

    private static int _pendingOperations;

    /// <summary>
    ///     Number of <see cref="LogAsync" /> operations currently in flight. Exposed for tests.
    /// </summary>
    internal static int PendingOperations => Volatile.Read(ref _pendingOperations);

    /// <summary>
    ///     Marks the logger as disposed. Subsequent <see cref="LogAsync" /> calls return
    ///     immediately. Idempotent.
    /// </summary>
    public static void Dispose()
    {
        using var scope = DisposeLock.EnterScope();
        if (IsDisposed) return;

        IsDisposed = true;
    }

    /// <summary>
    ///     Appends a formatted bug report (environment, error and exception details) to
    ///     the local log files and sends it to the remote bug report API.
    /// </summary>
    /// <param name="ex">The exception to report, or <see langword="null" /> for a message-only report.</param>
    /// <param name="contextMessage">A context message describing where the error occurred.</param>
    /// <param name="apiTimeoutSeconds">Overrides the API timeout; values &lt;= 0 or the default fall back to settings.</param>
    /// <returns>A task that completes when the report has been written and sent.</returns>
    public static async Task LogAsync(Exception? ex, string? contextMessage = null,
        int apiTimeoutSeconds = DefaultApiTimeoutSeconds)
    {
        if (IsDisposed) return;

        Interlocked.Increment(ref _pendingOperations);
        try
        {
            apiTimeoutSeconds = ResolveApiTimeoutSeconds(apiTimeoutSeconds);

            var bugReport = BugReportModel.FromException(ex, contextMessage);

            // Test-host reports are fake ("test", marker GUIDs). Drop them before they
            // are queued or written: they must never reach an older installed build,
            // which re-sends the pending queue at startup. The SendToApiOverride seam is
            // used by tests to inspect the generated report, so it must keep seeing them.
            if (SendToApiOverride == null && IsTestHostReport(bugReport)) return;

            var currentErrorMessage = bugReport.ToString();
            var pendingId = await EnqueuePendingReportAsync(bugReport);

            // Append to the log files first, while holding the lock only for the local
            // writes. The network send runs OUTSIDE the lock so that a burst of errors
            // does not serialize behind a slow (up to 30 s) API call.
            try
            {
                AppDataPaths.EnsureBaseDirectory();

                await LogFileLock.WaitAsync();
                try
                {
                    if (IsDisposed) return;

                    await AppendAllTextSharedAsync(UserLogFilePath, currentErrorMessage);
                    await AppendAllTextSharedAsync(ApiLogFilePath, currentErrorMessage);
                    await TrimLogFileAsync(UserLogFilePath);
                    await TrimLogFileAsync(ApiLogFilePath);
                }
                finally
                {
                    LogFileLock.Release();
                }
            }
            catch (Exception loggingEx)
            {
                _ = WriteInternalLogAsync("Failed to write log files.", loggingEx);
            }

            var sendResult = ApiSendResult.TransientFailure;
            try
            {
                sendResult = await SendLogToApiCoreAsync(bugReport, apiTimeoutSeconds);
            }
            catch (Exception loggingEx)
            {
                _ = WriteInternalLogAsync("Failed to send log to API.", loggingEx);
            }

            if (sendResult == ApiSendResult.Success)
            {
                try
                {
                    await RemovePendingReportAsync(pendingId);
                    await ClearSentReportAsync(currentErrorMessage);
                }
                catch (Exception loggingEx)
                {
                    _ = WriteInternalLogAsync("Failed to clear the sent report from the API log file.", loggingEx);
                }
            }
            else if (sendResult == ApiSendResult.PermanentFailure)
            {
                try
                {
                    // The API will never accept this payload; do not retry it forever.
                    await RemovePendingReportAsync(pendingId);
                }
                catch (Exception loggingEx)
                {
                    _ = WriteInternalLogAsync("Failed to drop a permanently rejected report from the queue.",
                        loggingEx);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingOperations);
        }
    }

    /// <summary>
    ///     Re-sends reports left in the pending queue by an earlier offline session. Called
    ///     once at startup; entries accepted by the API are removed from the queue.
    /// </summary>
    public static async Task RetryPendingReportsAsync()
    {
        if (IsDisposed) return;

        List<PendingReport> pending;
        await PendingQueueLock.WaitAsync();
        try
        {
            pending = await ReadPendingReportsAsync();
        }
        finally
        {
            PendingQueueLock.Release();
        }

        if (pending.Count == 0) return;

        var timeout = ResolveApiTimeoutSeconds(DefaultApiTimeoutSeconds);
        var delivered = new HashSet<string>(StringComparer.Ordinal);

        // Stale reports queued by an older test run must be dropped without attempting a
        // send. When an earlier real report failed transiently the loop stopped before
        // reaching them, so fake reports could stay in the bounded queue across launches.
        var rejected = new HashSet<string>(
            pending.Where(entry => IsTestHostReport(entry.Report)).Select(entry => entry.Id),
            StringComparer.Ordinal);

        foreach (var entry in pending)
        {
            if (IsDisposed) break;
            if (rejected.Contains(entry.Id)) continue;

            try
            {
                var result = await SendLogToApiCoreAsync(entry.Report, timeout);
                if (result == ApiSendResult.Success)
                {
                    delivered.Add(entry.Id);
                    continue;
                }

                if (result == ApiSendResult.PermanentFailure)
                {
                    // A 4xx rejection (invalid payload, deleted endpoint) will never
                    // succeed; keep it out of the queue instead of retrying forever.
                    rejected.Add(entry.Id);
                    continue;
                }

                // Transient network/timeout failure: stop hammering the API at startup.
                // The remaining entries stay queued for the next launch.
                break;
            }
            catch (Exception ex)
            {
                _ = WriteInternalLogAsync("A pending bug report could not be re-sent.", ex);
                break;
            }
        }

        if (delivered.Count == 0 && rejected.Count == 0) return;

        var ownsQueueMutex = TryEnterPendingQueueMutex();
        try
        {
            await PendingQueueLock.WaitAsync();
            try
            {
                var current = await ReadPendingReportsAsync();
                current.RemoveAll(entry => delivered.Contains(entry.Id) || rejected.Contains(entry.Id));
                await WritePendingReportsAsync(current);
            }
            finally
            {
                PendingQueueLock.Release();
            }
        }
        finally
        {
            ExitPendingQueueMutex(ownsQueueMutex);
        }
    }

    private static async Task<string?> EnqueuePendingReportAsync(BugReportModel report)
    {
        var id = Guid.NewGuid().ToString("N");
        var ownsQueueMutex = TryEnterPendingQueueMutex();

        try
        {
            await PendingQueueLock.WaitAsync();
            try
            {
                var entries = await ReadPendingReportsAsync();
                entries.Add(new PendingReport { Id = id, Report = report });

                // Bound the queue so a long offline period cannot grow it without limit.
                const int maxEntries = 200;
                if (entries.Count > maxEntries)
                    entries.RemoveRange(0, entries.Count - maxEntries);

                await WritePendingReportsAsync(entries);
                return id;
            }
            finally
            {
                PendingQueueLock.Release();
            }
        }
        finally
        {
            ExitPendingQueueMutex(ownsQueueMutex);
        }
    }

    private static async Task RemovePendingReportAsync(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;

        var ownsQueueMutex = TryEnterPendingQueueMutex();

        try
        {
            await PendingQueueLock.WaitAsync();
            try
            {
                var entries = await ReadPendingReportsAsync();
                if (entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.Ordinal)) > 0)
                    await WritePendingReportsAsync(entries);
            }
            finally
            {
                PendingQueueLock.Release();
            }
        }
        finally
        {
            ExitPendingQueueMutex(ownsQueueMutex);
        }
    }

    private static async Task<List<PendingReport>> ReadPendingReportsAsync()
    {
        try
        {
            if (!File.Exists(PendingReportsFilePath)) return [];

            var json = await ReadAllTextSharedAsync(PendingReportsFilePath);
            if (string.IsNullOrWhiteSpace(json)) return [];

            return JsonSerializer.Deserialize<List<PendingReport>>(json, CachedJsonSerializerOptions) ?? [];
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("Could not read the pending bug-report queue.", ex);
            return [];
        }
    }

    private static async Task WritePendingReportsAsync(List<PendingReport> entries)
    {
        try
        {
            AppDataPaths.EnsureBaseDirectory();
            var json = JsonSerializer.Serialize(entries, CachedJsonSerializerOptions);
            var tempPath = PendingReportsFilePath + ".tmp" + Guid.NewGuid().ToString("N")[..8];

            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, PendingReportsFilePath, true);
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("Could not write the pending bug-report queue.", ex);
        }
    }

    private sealed class PendingReport
    {
        public string Id { get; init; } = string.Empty;

        public BugReportModel Report { get; init; } = new();
    }

    private static Semaphore CreatePendingQueueMutex()
    {
        // A named semaphore serializes the read-modify-write queue across processes;
        // the in-process semaphore alone let a second instance overwrite the first
        // instance's queued reports.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PendingReportsFilePath)));
        return new Semaphore(1, 1, "FindRomCover.PendingBugReports." + hash[..16]);
    }

    private static bool TryEnterPendingQueueMutex()
    {
        try
        {
            return PendingQueueMutex.Value.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("Could not acquire the cross-process bug-report queue lock.", ex);
            return false;
        }
    }

    private static void ExitPendingQueueMutex(bool acquired)
    {
        if (!acquired) return;

        try
        {
            PendingQueueMutex.Value.Release();
        }
        catch (Exception ex)
        {
            Debug.Print($"Failed to release the bug-report queue semaphore: {ex.Message}");
        }
    }

    /// <summary>
    ///     Keeps the diagnostic log files bounded. The oldest half is removed at a report
    ///     boundary when the file grows past <see cref="MaxLogFileBytes" />.
    /// </summary>
    private static async Task TrimLogFileAsync(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= MaxLogFileBytes) return;

            var content = await ReadAllTextSharedAsync(path);
            var keepCharacters = content.Length / 2;
            var searchStart = Math.Max(0, content.Length - keepCharacters);
            var cut = content.IndexOf("BUG REPORT - FindRomCover", searchStart, StringComparison.Ordinal);
            if (cut < 0) cut = searchStart;

            // Write through a temp file and replace atomically: a crash or disk-full
            // during an in-place rewrite would truncate the pending report queue.
            var tempPath = path + ".tmp" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                await File.WriteAllTextAsync(tempPath, content[cut..]);
                File.Move(tempPath, path, true);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                    // Best effort cleanup.
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            // Never route an internal-log trim failure back through WriteInternalLogAsync:
            // that method appends to and trims the same file, creating an unbounded
            // write/trim retry loop when the trim keeps failing.
            if (string.Equals(path, InternalLogFilePath, StringComparison.Ordinal))
                Debug.Print($"Failed to trim the internal log file: {ex}");
            else
                _ = WriteInternalLogAsync("Failed to trim a diagnostic log file.", ex);
        }
    }

    /// <summary>
    ///     Removes only the report that was successfully sent from the pending API log
    ///     file, preserving reports appended by concurrent or earlier failed sends.
    /// </summary>
    /// <param name="sentReport">The exact report text that the API accepted.</param>
    private static async Task ClearSentReportAsync(string sentReport)
    {
        if (string.IsNullOrEmpty(sentReport)) return;

        await LogFileLock.WaitAsync();
        try
        {
            if (!File.Exists(ApiLogFilePath)) return;

            var content = await ReadAllTextSharedAsync(ApiLogFilePath);
            var index = content.IndexOf(sentReport, StringComparison.Ordinal);
            if (index < 0) return;

            var remaining = content.Remove(index, sentReport.Length);
            var tempPath = ApiLogFilePath + ".tmp" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                // Write the new content to a temp file and replace atomically. A crash
                // or disk-full during WriteAllTextAsync would otherwise truncate the
                // file before the remaining pending reports are written.
                await File.WriteAllTextAsync(tempPath, remaining);
                File.Move(tempPath, ApiLogFilePath, true);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    _ = WriteInternalLogAsync("Failed to remove a temporary report file.", cleanupEx);
                }

                throw;
            }
        }
        finally
        {
            LogFileLock.Release();
        }
    }

    private static int ResolveApiTimeoutSeconds(int apiTimeoutSeconds)
    {
        try
        {
            if (apiTimeoutSeconds > 0 && apiTimeoutSeconds != DefaultApiTimeoutSeconds) return apiTimeoutSeconds;

            return SettingsManager.CurrentInstance?.ApiTimeoutSeconds ?? DefaultApiTimeoutSeconds;
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("Failed to resolve API timeout from settings.", ex);
            return apiTimeoutSeconds > 0 ? apiTimeoutSeconds : DefaultApiTimeoutSeconds;
        }
    }

    private enum ApiSendResult
    {
        Success,
        TransientFailure,
        PermanentFailure
    }

    /// <summary>
    ///     Directory names that identify a test host. Reports created there must never
    ///     reach the production bug tracker.
    /// </summary>
    private static readonly string[] TestHostDirectoryNames = ["FindRomCover.Tests", "testhost"];

    /// <summary>
    ///     Gets a value indicating whether the given report was created by a test host.
    ///     Exposed for tests.
    /// </summary>
    /// <param name="report">The report to inspect.</param>
    /// <returns><see langword="true" /> when the report comes from a test host.</returns>
    internal static bool IsTestHostReport(BugReportModel report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return IsTestHostBaseDirectory(report.BaseDirectory);
    }

    /// <summary>
    ///     Gets a value indicating whether a base directory belongs to a test host.
    ///     Exposed for tests.
    /// </summary>
    /// <param name="baseDirectory">The base directory recorded in a bug report.</param>
    /// <returns><see langword="true" /> when a path segment names a test host.</returns>
    internal static bool IsTestHostBaseDirectory(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory)) return false;

        return baseDirectory
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => TestHostDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<ApiSendResult> SendLogToApiCoreAsync(BugReportModel bugReport, int apiTimeoutSeconds)
    {
        if (IsDisposed) return ApiSendResult.TransientFailure;

        if (SendToApiOverride != null)
            return await SendToApiOverride(bugReport, apiTimeoutSeconds)
                ? ApiSendResult.Success
                : ApiSendResult.TransientFailure;

        // Reports produced by a test host are fake ("test", marker GUIDs) and used to be
        // uploaded by the installed application when it retried a queue leaked by a test
        // run. Treat them as permanently rejected so they are dropped from the queue.
        if (IsTestHostReport(bugReport)) return ApiSendResult.PermanentFailure;

        try
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("=== Environment Details ===");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Date: {bugReport.Date:yyyy-MM-dd HH:mm:ss}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {bugReport.ApplicationName}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture,
                $"Application Version: {bugReport.ApplicationVersion}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {bugReport.OsVersion}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {bugReport.Architecture}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Bitness: {bugReport.Bitness}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture,
                $"Windows/Linux/macOS Version: {bugReport.WindowsVersion}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {bugReport.ProcessorCount}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {bugReport.BaseDirectory}");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {bugReport.TempPath}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("=== Error Details ===");
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Error Message: {bugReport.ErrorMessage}");

            var message = messageBuilder.ToString();
            if (message.Length > 4000) message = message[..3997] + "...";

            var stackTraceBuilder = new StringBuilder();
            stackTraceBuilder.AppendLine("=== Exception Details ===");
            stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture, $"Type: {bugReport.Exception.Type}");
            stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture, $"Message: {bugReport.Exception.Message}");
            stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture, $"Source: {bugReport.Exception.Source}");
            stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {bugReport.Exception.StackTrace}");

            if (bugReport.Exception.InnerException != null)
            {
                stackTraceBuilder.AppendLine();
                stackTraceBuilder.AppendLine("--- Inner Exception ---");
                stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"Type: {bugReport.Exception.InnerException.Type}");
                stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"Message: {bugReport.Exception.InnerException.Message}");
                stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"Source: {bugReport.Exception.InnerException.Source}");
                stackTraceBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"StackTrace: {bugReport.Exception.InnerException.StackTrace}");
            }

            var stackTrace = stackTraceBuilder.ToString();
            if (stackTrace.Length > 8000) stackTrace = stackTrace[..7997] + "...";

            var bugReportPayload = new
            {
                message,
                applicationName = bugReport.ApplicationName,
                version = bugReport.ApplicationVersion,
                environment = bugReport.OsVersion,
                stackTrace,
                userInfo = $"Arch: {bugReport.Architecture}, {bugReport.Bitness}"
            };

            var jsonPayload = JsonSerializer.Serialize(bugReportPayload, CachedJsonSerializerOptions);

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, BugReportApiUrl);
            request.Content = content;
            request.Headers.Add("X-API-KEY", ApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(apiTimeoutSeconds));
            using var response = await HttpClientHelper.Client.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode) return ApiSendResult.Success;

            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            _ = WriteInternalLogAsync(
                $"API returned non-success status code: {response.StatusCode}. Response: {errorContent}");
            return IsPermanentApiFailure(response.StatusCode)
                ? ApiSendResult.PermanentFailure
                : ApiSendResult.TransientFailure;
        }
        catch (HttpRequestException httpEx)
        {
            _ = WriteInternalLogAsync("HTTP request failed when sending log to API.", httpEx);
            return ApiSendResult.TransientFailure;
        }
        catch (TaskCanceledException tcEx) when (tcEx.CancellationToken.IsCancellationRequested)
        {
            _ = WriteInternalLogAsync("HTTP Request timed out when sending log to API.");
            return ApiSendResult.TransientFailure;
        }
        catch (TaskCanceledException)
        {
            _ = WriteInternalLogAsync("HTTP Request was cancelled or timed out (HttpClient internal timeout).");
            return ApiSendResult.TransientFailure;
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("An unexpected error occurred when sending log to API.", ex);
            return ApiSendResult.TransientFailure;
        }
    }

    private static bool IsPermanentApiFailure(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code is < 400 or >= 500) return false;

        // These 4xx codes are explicitly retryable; every other 4xx is a permanent
        // payload/auth rejection.
        return code is not (408 or 425 or 429);
    }

    /// <summary>
    ///     Appends text without taking an exclusive lock, so a second instance can write
    ///     the same diagnostic file instead of failing with a sharing violation.
    /// </summary>
    private static async Task AppendAllTextSharedAsync(string path, string content)
    {
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        await using var writer = new StreamWriter(stream, Utf8NoBom);
        await writer.WriteAsync(content);
    }

    /// <summary>
    ///     Reads a file while allowing concurrent writers, instead of failing with a
    ///     sharing violation when another instance is appending to it.
    /// </summary>
    private static async Task<string> ReadAllTextSharedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteInternalLogAsync(string message, Exception? exception = null)
    {
        try
        {
            var logMessage = new StringBuilder();
            logMessage.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");

            if (exception != null)
            {
                logMessage.AppendLine(CultureInfo.InvariantCulture, $"{exception.GetType().Name}: {exception.Message}");

                if (!string.IsNullOrWhiteSpace(exception.StackTrace)) logMessage.AppendLine(exception.StackTrace);
            }

            await InternalLogSemaphore.WaitAsync();
            try
            {
                await AppendAllTextSharedAsync(InternalLogFilePath, logMessage.ToString());
                // Unlike the two report logs, this file used to grow without bound on a
                // machine that stays offline.
                await TrimLogFileAsync(InternalLogFilePath);
            }
            finally
            {
                InternalLogSemaphore.Release();
            }
        }
        catch
        {
            Debug.Print("Failed to write to internal log file.");
        }
    }
}
