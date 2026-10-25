using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FindRomCover.Models;

/// <summary>
///     Collects application, environment, and exception details for a generated bug report.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global",
    Justification = "Public init accessors are required by JSON serialization.")]
public class BugReportModel
{
    /// <summary>Name of the application.</summary>
    public string ApplicationName { get; } = "FindRomCover";

    /// <summary>Date and time the report was created.</summary>
    public DateTime Date { get; init; } = DateTime.Now;

    /// <summary>Version of the running application.</summary>
    public string ApplicationVersion { get; init; } = "Unknown";

    /// <summary>Description of the operating system.</summary>
    public string OsVersion { get; init; } = "Unknown";

    /// <summary>Architecture of the operating system (for example, X64).</summary>
    public string Architecture { get; init; } = "Unknown";

    /// <summary>Whether the operating system is 32-bit or 64-bit.</summary>
    public string Bitness { get; init; } = "Unknown";

    /// <summary>Friendly name of the Windows version.</summary>
    public string WindowsVersion { get; init; } = "Unknown";

    /// <summary>Number of logical processors available to the process.</summary>
    public int ProcessorCount { get; init; }

    /// <summary>Base directory of the running application.</summary>
    public string BaseDirectory { get; init; } = "Unknown";

    /// <summary>Path of the system temporary directory.</summary>
    public string TempPath { get; init; } = "Unknown";

    /// <summary>Context message describing what was being done when the error occurred.</summary>
    public string ErrorMessage { get; init; } = "";

    /// <summary>Details of the captured exception.</summary>
    public ExceptionDetails Exception { get; init; } = new();

    /// <summary>
    ///     Creates a bug report from the given exception, capturing the current environment.
    /// </summary>
    /// <param name="ex">The exception to capture, or <see langword="null" /> when none is available.</param>
    /// <param name="contextMessage">Optional context message describing the failing operation.</param>
    /// <returns>A populated <see cref="BugReportModel" />.</returns>
    public static BugReportModel FromException(Exception? ex, string? contextMessage = null)
    {
        var model = new BugReportModel
        {
            Date = DateTime.Now,
            ApplicationVersion = GetApplicationVersion(),
            OsVersion = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Bitness = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
            WindowsVersion = GetFriendlyWindowsVersion(),
            ProcessorCount = Environment.ProcessorCount,
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
            TempPath = Path.GetTempPath(),
            ErrorMessage = contextMessage ?? "",
            Exception = ex != null ? ExceptionDetails.FromException(ex) : new ExceptionDetails()
        };

        return model;
    }

    /// <summary>
    ///     Renders the report as a plain-text block suitable for display or saving.
    /// </summary>
    /// <returns>The formatted report text.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("                           BUG REPORT - FindRomCover");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        sb.AppendLine("=== Environment Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {Date:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {ApplicationName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Application Version: {ApplicationVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {OsVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {Architecture}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Bitness: {Bitness}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Windows/Linux/macOS Version: {WindowsVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {ProcessorCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {BaseDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {TempPath}");
        sb.AppendLine();

        sb.AppendLine("=== Error Details ===");
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Error Message: {ErrorMessage}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(Exception.Type))
        {
            sb.AppendLine("=== Exception Details ===");
            sb.AppendLine(Exception.ToString());
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string GetApplicationVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    private static string GetFriendlyWindowsVersion()
    {
        var os = Environment.OSVersion;

        if (OperatingSystem.IsWindows())
            return os.Version.Major switch
            {
                >= 10 => os.Version.Build >= 22000 ? "Windows 11" : "Windows 10",
                6 => os.Version.Minor switch
                {
                    1 => "Windows 7",
                    2 => "Windows 8",
                    3 => "Windows 8.1",
                    _ => "Older Windows NT"
                },
                _ => "Older Windows NT"
            };

        if (OperatingSystem.IsLinux()) return $"Linux {os.Version}";
        if (OperatingSystem.IsMacOS()) return $"macOS {os.Version}";
        if (OperatingSystem.IsFreeBSD()) return $"FreeBSD {os.Version}";

        return os.Platform.ToString();
    }
}
