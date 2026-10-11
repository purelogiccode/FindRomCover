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

    private static readonly SemaphoreSlim LogFileLock = new(1, 1);
    private static readonly SemaphoreSlim InternalLogSemaphore = new(1, 1);

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

            await File.WriteAllTextAsync(ApiLogFilePath, content.Remove(index, sentReport.Length));
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
            messageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Windows/Linux/macOS Version: {bugReport.WindowsVersion}");
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