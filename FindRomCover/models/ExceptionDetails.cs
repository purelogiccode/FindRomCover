using System.Globalization;
using System.Text;

namespace FindRomCover.Models;

public class ExceptionDetails
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string Source { get; set; } = "";
    public string StackTrace { get; set; } = "";
    public ExceptionDetails? InnerException { get; set; }

    public static ExceptionDetails FromException(Exception ex)
    {
        var details = new ExceptionDetails
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            Source = ex.Source ?? "Unknown",
            StackTrace = string.IsNullOrWhiteSpace(ex.StackTrace) ? "Unavailable" : ex.StackTrace
        };

        if (ex.InnerException != null) details.InnerException = FromException(ex.InnerException);

        return details;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {Type}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {Message}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Source: {Source}");
        sb.AppendLine("StackTrace:");
        sb.AppendLine(StackTrace);

        if (InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("--- Inner Exception ---");
            sb.AppendLine(InnerException.ToString());
        }

        return sb.ToString();
    }
}