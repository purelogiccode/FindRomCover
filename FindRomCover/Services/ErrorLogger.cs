using System.Diagnostics;
using System.Globalization;
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

    /// <summary>Path of the log file holding reports not yet accepted by the API.</summary>
    internal static readonly string ApiLogFilePath = AppDataPaths.LogFilePath("ApiLogError.txt");

    /// <summary>Path of the log file holding all user-facing error reports.</summary>
    internal static readonly string UserLogFilePath = AppDataPaths.LogFilePath("UserLogError.txt");

    /// <summary>Path of the log file holding internal diagnostics of the logger itself.</summary>
    internal static readonly string InternalLogFilePath = AppDataPaths.LogFilePath("InternalLog.txt");

    /// <summary>Path of the JSON queue used to retry reports that could not be sent.</summary>
    internal static readonly string PendingReportsFilePath = AppDataPaths.LogFilePath("PendingBugReports.json");

    private static readonly SemaphoreSlim LogFileLock = new(1, 1);
    private static readonly SemaphoreSlim PendingQueueLock = new(1, 1);
    private static readonly SemaphoreSlim InternalLogSemaphore = new(1, 1);
    private const long MaxLogFileBytes = 4L * 1024 * 1024;

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

                    await File.AppendAllTextAsync(UserLogFilePath, currentErrorMessage);
                    await File.AppendAllTextAsync(ApiLogFilePath, currentErrorMessage);
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

            var sendSuccess = false;
            try
            {
                sendSuccess = await SendLogToApiAsync(bugReport, apiTimeoutSeconds);
            }
            catch (Exception loggingEx)
            {
                _ = WriteInternalLogAsync("Failed to send log to API.", loggingEx);
            }

            if (sendSuccess)
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

        foreach (var entry in pending)
        {
            if (IsDisposed) break;

            try
            {
                if (await SendLogToApiAsync(entry.Report, timeout)) delivered.Add(entry.Id);
            }
            catch (Exception ex)
            {
                _ = WriteInternalLogAsync("A pending bug report could not be re-sent.", ex);
            }
        }

        if (delivered.Count == 0) return;

        await PendingQueueLock.WaitAsync();
        try
        {
            var current = await ReadPendingReportsAsync();
            current.RemoveAll(entry => delivered.Contains(entry.Id));
            await WritePendingReportsAsync(current);
        }
        finally
        {
            PendingQueueLock.Release();
        }
    }

    private static async Task<string?> EnqueuePendingReportAsync(BugReportModel report)
    {
        var id = Guid.NewGuid().ToString("N");

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

    private static async Task RemovePendingReportAsync(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;

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

    private static async Task<List<PendingReport>> ReadPendingReportsAsync()
    {
        try
        {
            if (!File.Exists(PendingReportsFilePath)) return [];

            var json = await File.ReadAllTextAsync(PendingReportsFilePath);
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
        public string Id { get; set; } = string.Empty;

        public BugReportModel Report { get; set; } = new();
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

            var content = await File.ReadAllTextAsync(path);
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

            var content = await File.ReadAllTextAsync(ApiLogFilePath);
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

    private static async Task<bool> SendLogToApiAsync(BugReportModel bugReport, int apiTimeoutSeconds)
    {
        if (IsDisposed) return false;

        if (SendToApiOverride != null) return await SendToApiOverride(bugReport, apiTimeoutSeconds);

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

            if (response.IsSuccessStatusCode) return true;

            var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
            _ = WriteInternalLogAsync(
                $"API returned non-success status code: {response.StatusCode}. Response: {errorContent}");
            return false;
        }
        catch (HttpRequestException httpEx)
        {
            _ = WriteInternalLogAsync("HTTP request failed when sending log to API.", httpEx);
            return false;
        }
        catch (TaskCanceledException tcEx) when (tcEx.CancellationToken.IsCancellationRequested)
        {
            _ = WriteInternalLogAsync("HTTP Request timed out when sending log to API.");
            return false;
        }
        catch (TaskCanceledException)
        {
            _ = WriteInternalLogAsync("HTTP Request was cancelled or timed out (HttpClient internal timeout).");
            return false;
        }
        catch (Exception ex)
        {
            _ = WriteInternalLogAsync("An unexpected error occurred when sending log to API.", ex);
            return false;
        }
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
                await File.AppendAllTextAsync(InternalLogFilePath, logMessage.ToString());
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