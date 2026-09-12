using System.Diagnostics;
using System.Globalization;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace FindRomCover.Services;

public class BugReportSink : ILogEventSink
{
    private readonly ITextFormatter? _formatter;

    public BugReportSink(ITextFormatter? formatter)
    {
        _formatter = formatter;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error)
            return;

        try
        {
            var contextMessage = FormatMessage(logEvent);
            var ex = logEvent.Exception;

            if (IsTransientWatcherError(ex, contextMessage))
                return;

            if (ex == null && logEvent.Level >= LogEventLevel.Error) ex = new InvalidOperationException(contextMessage);

            _ = ErrorLogger.LogAsync(ex, contextMessage).ContinueWith(
                static t =>
                {
                    if (t.IsFaulted)
                        Debug.Print($"BugReportSink: ErrorLogger.LogAsync failed: {t.Exception?.InnerException}");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception sinkEx)
        {
            Debug.Print($"BugReportSink: failed to emit log event: {sinkEx.Message}");
        }
    }

    private string FormatMessage(LogEvent logEvent)
    {
        if (_formatter != null)
        {
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            return writer.ToString();
        }

        return logEvent.RenderMessage(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// FileSystemWatcher infrastructure failures (buffer overflow, access denied,
    /// drive disconnected, AV lock, ...) are environmental — not application bugs.
    /// Suppress them so they never create automatic bug reports (see issue #66867:
    /// Win32Exception (5) "Accesso negato" from ImageFolderWatcher).
    /// </summary>
    private static bool IsTransientWatcherError(Exception? ex, string contextMessage)
    {
        if (string.IsNullOrEmpty(contextMessage))
            return false;

        if (!contextMessage.Contains("ImageFolderWatcher", StringComparison.OrdinalIgnoreCase) &&
            !contextMessage.Contains("FileSystemWatcher", StringComparison.OrdinalIgnoreCase))
            return false;

        return ex is InternalBufferOverflowException
            or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException
            or IOException;
    }
}