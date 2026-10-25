using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace FindRomCover.Services;

/// <summary>
///     Serilog sink that forwards every Warning-or-higher event to the remote bug
///     report API through <see cref="ErrorLogger" />. Known environmental failures
///     (file-system watcher errors, network hiccups, ...) are suppressed so that a
///     temporarily offline machine does not flood the bug tracker.
/// </summary>
internal sealed class BugReportSink : ILogEventSink
{
    private static readonly long ThrottleWindowTicks = TimeSpan.FromMinutes(1).Ticks;
    private readonly ITextFormatter? _formatter;
    private readonly ConcurrentDictionary<string, long> _recentReports = new(StringComparer.Ordinal);

    /// <summary>
    ///     Creates the sink.
    /// </summary>
    /// <param name="formatter">Formatter used to render the log event into the bug report message.</param>
    public BugReportSink(ITextFormatter? formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    ///     Gets a value indicating whether an event at the given level should be forwarded
    ///     to the bug report API. Every warning or higher is forwarded.
    /// </summary>
    /// <param name="level">The Serilog event level.</param>
    /// <returns><see langword="true" /> when the event must be forwarded.</returns>
    internal static bool IsForwardable(LogEventLevel level)
    {
        return level >= LogEventLevel.Warning;
    }

    /// <summary>
    ///     Emits the log event. Events below Warning are ignored.
    /// </summary>
    /// <param name="logEvent">The Serilog event to process.</param>
    public void Emit(LogEvent logEvent)
    {
        if (!IsForwardable(logEvent.Level))
            return;

        try
        {
            var contextMessage = FormatMessage(logEvent);
            var ex = logEvent.Exception;

            if (IsTransientWatcherError(logEvent, ex, contextMessage))
                return;

            if (IsTransientEnvironmentError(logEvent))
                return;

            var throttleKey = $"{logEvent.Level}|{contextMessage}";
            var nowTicks = DateTime.UtcNow.Ticks;
            if (_recentReports.TryGetValue(throttleKey, out var lastTicks) &&
                nowTicks - lastTicks < ThrottleWindowTicks)
                return;

            _recentReports[throttleKey] = nowTicks;
            PruneThrottleEntries(nowTicks);

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

    private void PruneThrottleEntries(long nowTicks)
    {
        if (_recentReports.Count < 500) return;

        foreach (var pair in _recentReports)
            if (nowTicks - pair.Value >= ThrottleWindowTicks)
                _recentReports.TryRemove(pair.Key, out _);
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
    ///     Network and hardware dependent failures (the application stats endpoint,
    ///     the GitHub update check, missing audio device, ...) are environmental and
    ///     must never create automatic bug reports. Callers mark such events with
    ///     <see cref="LogService.TransientErrorPropertyName" />.
    /// </summary>
    private static bool IsTransientEnvironmentError(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue(LogService.TransientErrorPropertyName, out var property) &&
               property is ScalarValue { Value: bool isTransient } && isTransient;
    }

    /// <summary>
    ///     FileSystemWatcher infrastructure failures (buffer overflow, access denied,
    ///     drive disconnected, AV lock, ...) are environmental — not application bugs.
    ///     Suppress them so they never create automatic bug reports (see issue #66867:
    ///     Win32Exception (5) "Accesso negato" from ImageFolderWatcher).
    /// </summary>
    private static bool IsTransientWatcherError(LogEvent logEvent, Exception? ex, string contextMessage)
    {
        // Structured marker: per-file watcher processing failures (corrupt image,
        // locked file, dropped file) are marked by LogService.ErrorWatcher and must
        // never file bug reports — independent of message wording.
        if (logEvent.Properties.TryGetValue(LogService.WatcherErrorPropertyName, out var property) &&
            property is ScalarValue { Value: bool isWatcherError } && isWatcherError)
            return true;

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