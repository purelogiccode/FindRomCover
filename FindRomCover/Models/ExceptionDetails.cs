using System.Globalization;
using System.Text;

namespace FindRomCover.Models;

/// <summary>
///     Captured details of an exception, including nested inner exceptions.
/// </summary>
public class ExceptionDetails
{
    /// <summary>Full type name of the exception.</summary>
    public string Type { get; set; } = "";

    /// <summary>Message of the exception.</summary>
    public string Message { get; set; } = "";

    /// <summary>Name of the source where the exception was raised.</summary>
    public string Source { get; set; } = "";

    /// <summary>Stack trace of the exception, or "Unavailable" when none was captured.</summary>
    public string StackTrace { get; set; } = "";

    /// <summary>Details of the inner exception, or <see langword="null" /> when there is none.</summary>
    public ExceptionDetails? InnerException { get; set; }

    /// <summary>
    ///     Builds exception details from the given exception, recursively capturing inner exceptions.
    /// </summary>
    /// <param name="ex">The exception to capture.</param>
    /// <returns>A populated <see cref="ExceptionDetails" /> instance.</returns>
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

    /// <summary>
    ///     Renders the details, including any inner exception, as plain text.
    /// </summary>
    /// <returns>The formatted exception text.</returns>
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