using System.IO;

namespace FindRomCover.Services;

/// <summary>
///     Central location for per-user application data. Logs, diagnostics and screenshots
///     must NOT live under the install directory (unwritable on Program Files installs),
///     so everything writable goes to %LocalAppData%\FindRomCover.
/// </summary>
public static class AppDataPaths
{
    public static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover");

    public static string LogFilePath(string fileName)
    {
        return Path.Combine(BaseDirectory, fileName);
    }

    public static void EnsureBaseDirectory()
    {
        if (!Directory.Exists(BaseDirectory)) Directory.CreateDirectory(BaseDirectory);
    }
}