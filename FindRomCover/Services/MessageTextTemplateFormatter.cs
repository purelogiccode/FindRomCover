using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;

namespace FindRomCover.Services;

/// <summary>
///     Formats a Serilog event as a single plain-text line with timestamp, level,
///     rendered message and exception, matching the layout of the log files.
/// </summary>
internal sealed class MessageTextTemplateFormatter : ITextFormatter
{
    /// <summary>
    ///     Writes the formatted event to the given writer.
    /// </summary>
    /// <param name="logEvent">The event to format.</param>
    /// <param name="output">The destination writer.</param>
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        var timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = logEvent.Level.ToString().ToUpperInvariant();

        output.Write(timestamp);
        output.Write(" [");
        output.Write(level);
        output.Write("] ");
        output.Write(message);

        if (logEvent.Exception != null)
        {
            output.WriteLine();
            output.Write(logEvent.Exception);
        }
    }
}