namespace FindRomCover.Services;

/// <summary>
///     Central location for per-user application data. Logs, diagnostics and screenshots
///     must NOT live under the install directory (unwritable on Program Files installs),
///     so everything writable goes to %LocalAppData%\FindRomCover.
/// </summary>
public static class AppDataPaths
{
    private static string? _baseDirectoryOverride;

    /// <summary>
    ///     Test seam that redirects every writable path to a temporary directory. Exposed
    ///     for tests: without it a test run writes its fake bug reports into the real
    ///     pending-report queue, and the next application launch uploads them to the
    ///     production bug tracker.
    /// </summary>
    /// <remarks>
    ///     Once a temporary directory has been assigned the override cannot be cleared.
    ///     A test that cleared it used to make concurrent logger tests write their fake
    ///     "test"/marker reports into the real %LocalAppData%\FindRomCover queue, which
    ///     the installed application then uploaded to the production tracker.
    /// </remarks>
    internal static string? BaseDirectoryOverride
    {
        get => _baseDirectoryOverride;
        set
        {
            if (value is { Length: > 0 }) _baseDirectoryOverride = value;
        }
    }

    /// <summary>
    ///     Gets the per-user application data folder (%LocalAppData%\FindRomCover).
    ///     Falls back to the user profile or the temp folder when the platform does
    ///     not report a local application-data directory (minimal Linux sessions).
    /// </summary>
    public static string BaseDirectory =>
        _baseDirectoryOverride is { Length: > 0 } overrideDirectory
            ? overrideDirectory
            : DefaultBaseDirectory;

    /// <summary>
    ///     Gets the real per-user application data folder, ignoring the test override.
    ///     Exposed for tests so the fallback can be verified without clearing the seam.
    /// </summary>
    internal static string DefaultBaseDirectory => Path.Combine(GetWritableRoot(), "FindRomCover");

    private static string GetWritableRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();

        return root;
    }

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
