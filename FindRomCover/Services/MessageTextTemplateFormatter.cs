using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace FindRomCover.Services;

public class MessageTextTemplateFormatter : ITextFormatter
{
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