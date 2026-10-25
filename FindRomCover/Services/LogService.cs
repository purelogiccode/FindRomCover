using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FindRomCover.Models;
using Serilog;
using Serilog.Events;

namespace FindRomCover.Services;

/// <summary>
///     Central application logging facade. All application code must log through this
///     class; the underlying Serilog pipeline writes to the rolling log file, to the
///     in-app <see cref="DebugWindow" /> collection and to the remote bug report API
///     (through <see cref="BugReportSink" />).
/// </summary>
public static class LogService
{
    /// <summary>
    ///     Structured property name used to mark per-file image-folder watcher failures.
    ///     Events carrying this property are never forwarded to the bug report API.
    /// </summary>
    public const string WatcherErrorPropertyName = "WatcherError";

    /// <summary>
    ///     Structured property name used to mark environmental failures (network,
    ///     audio, update check, ...). Events carrying this property are never forwarded
    ///     to the bug report API.
    /// </summary>
    public const string TransientErrorPropertyName = "TransientEnvironmentError";

    /// <summary>
    ///     Set by the GUI head (Avalonia) to marshal log entries onto the UI thread.
    ///     When null, entries are appended directly.
    /// </summary>
    public static Action<Action>? UiDispatcher { get; set; }

    private static ILogger _logger = new SilentLogger();
    private static readonly ObservableCollection<LogEntry> LogMessages = new();
    private static readonly Lock Lock = new();
    private static bool _initialized;

    /// <summary>
    ///     Gets the live collection of log entries displayed by <see cref="DebugWindow" />.
    /// </summary>
    /// <returns>The observable collection of log entries (maximum 5000 entries).</returns>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField",
        Justification = "The live collection is returned for UI binding. Log mutations are marshalled to the UI thread by UiDispatcher; the collection lock only protects early or dispatcher-less writers.")]
    public static ObservableCollection<LogEntry> GetLogMessages()
    {
        return LogMessages;
    }

    /// <summary>
    ///     Initializes the Serilog pipeline once. Writes to a daily rolling file under
    ///     the per-user application data folder, to the in-app log window and to the
    ///     remote bug report API.
    /// </summary>
    public static void Initialize()
    {
        lock (Lock)
        {
            if (_initialized) return;

            try
            {
                AppDataPaths.EnsureBaseDirectory();
                var logFilePath = AppDataPaths.LogFilePath("app.log");

                // shared: allow a second instance (or a tool that holds the file) without
                // aborting startup with an IOException.
                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        logFilePath,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        shared: true)
                    .WriteTo.Observers(events => events.Subscribe(new LogEventObserver()))
                    .WriteTo.Sink(new BugReportSink(new MessageTextTemplateFormatter()))
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                // Logging must never be fatal: an unwritable data folder or a locked log
                // file should not stop the application from starting.
                _logger = new SilentLogger();
                System.Diagnostics.Debug.Print($"LogService: could not initialize file logging: {ex}");
            }

            _initialized = true;
        }
    }

    private static void OnLogEvent(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        var level = logEvent.Level;
        var timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var logEntryText = $"{timestamp} [{level}] {message}";

        if (logEvent.Exception != null) logEntryText += $"\n{logEvent.Exception}";

        var logEntry = new LogEntry { Message = logEntryText };

        var dispatch = UiDispatcher;
        if (dispatch != null)
            dispatch(() => AppendLogEntry(logEntry));
        else
            AppendLogEntry(logEntry);
    }

    private static void AppendLogEntry(LogEntry logEntry)
    {
        // Serilog observer callbacks can run on arbitrary threads (and concurrently
        // when no UI dispatcher is installed), so the shared collection must be
        // mutated under a lock.
        lock (LogMessages)
        {
            LogMessages.Add(logEntry);
            while (LogMessages.Count > 5000) LogMessages.RemoveAt(0);
        }
    }

    /// <summary>
    ///     Writes a debug-level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Debug(string message)
    {
        _logger.Debug(message);
    }

    /// <summary>
    ///     Writes an information-level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Information(string message)
    {
        _logger.Information(message);
    }

    /// <summary>
    ///     Writes a warning-level message. Warnings are forwarded to the bug report API
    ///     unless marked as transient (see <see cref="WarningTransient(string)" />).
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Warning(string message)
    {
        _logger.Warning(message);
    }

    /// <summary>
    ///     Writes a warning-level message with an exception. Warnings are forwarded to
    ///     the bug report API unless marked as transient.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The context message.</param>
    public static void Warning(Exception ex, string message)
    {
        _logger.Warning(ex, message);
    }

    /// <summary>
    ///     Writes a warning-level message that is expected in a degraded environment
    ///     (no network, no audio device, ...). These warnings stay in the log file and
    ///     log window but are never forwarded to the bug report API.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void WarningTransient(string message)
    {
        _logger.ForContext(TransientErrorPropertyName, true).Warning(message);
    }

    /// <summary>
    ///     Writes a warning-level message with an exception that is expected in a
    ///     degraded environment (no network, no audio device, ...). These warnings stay
    ///     in the log file and log window but are never forwarded to the bug report API.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The context message.</param>
    public static void WarningTransient(Exception ex, string message)
    {
        _logger.ForContext(TransientErrorPropertyName, true).Warning(ex, message);
    }

    /// <summary>
    ///     Writes an error-level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Error(string message)
    {
        _logger.Error(message);
    }

    /// <summary>
    ///     Writes an error-level message with an exception.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The context message.</param>
    public static void Error(Exception ex, string message)
    {
        _logger.Error(ex, message);
    }

    /// <summary>
    ///     Logs an error raised while processing a user file in the image-folder watcher
    ///     (corrupt image, locked file, dropped file, ...). These are environmental
    ///     per-file failures, not application bugs — the <see cref="BugReportSink"/>
    ///     suppresses automatic bug reports for events marked with
    ///     <see cref="WatcherErrorPropertyName"/>.
    /// </summary>
    /// <param name="ex">The exception raised while processing the file.</param>
    /// <param name="message">The context message.</param>
    public static void ErrorWatcher(Exception ex, string message)
    {
        _logger.ForContext(WatcherErrorPropertyName, true).Error(ex, message);
    }

    /// <summary>
    ///     Writes a fatal-level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Fatal(string message)
    {
        _logger.Fatal(message);
    }

    /// <summary>
    ///     Writes a fatal-level message with an exception.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The context message.</param>
    public static void Fatal(Exception ex, string message)
    {
        _logger.Fatal(ex, message);
    }

    /// <summary>
    ///     Flushes and disposes the Serilog pipeline and resets the initialization flag.
    /// </summary>
    public static void Dispose()
    {
        lock (Lock)
        {
            if (_logger is IDisposable disposable) disposable.Dispose();

            _initialized = false;
        }
    }

    private sealed class LogEventObserver : IObserver<LogEvent>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            System.Diagnostics.Debug.Print($"Serilog LogEventObserver error: {error}");
        }

        public void OnNext(LogEvent value)
        {
            OnLogEvent(value);
        }
    }

    private sealed class SilentLogger : ILogger
    {
        public void Write(LogEvent logEvent)
        {
        }
    }
}