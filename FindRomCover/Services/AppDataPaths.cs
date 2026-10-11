namespace FindRomCover.Services;

/// <summary>
///     Central location for per-user application data. Logs, diagnostics and screenshots
///     must NOT live under the install directory (unwritable on Program Files installs),
///     so everything writable goes to %LocalAppData%\FindRomCover.
/// </summary>
public static class AppDataPaths
{
    /// <summary>
    ///     Gets the per-user application data folder (%LocalAppData%\FindRomCover).
    /// </summary>
    public static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FindRomCover");

    /// <summary>
    ///     Gets the full path of a file inside the per-user application data folder.
    /// </summary>
    /// <param name="fileName">The plain file name.</param>
    /// <returns>The combined full path.</returns>
    public static string LogFilePath(string fileName)
    {
        return Path.Combine(BaseDirectory, fileName);
    }

    /// <summary>
    ///     Creates the per-user application data folder when it does not exist yet.
    /// </summary>
    public static void EnsureBaseDirectory()
    {
        if (!Directory.Exists(BaseDirectory)) Directory.CreateDirectory(BaseDirectory);
    }
}