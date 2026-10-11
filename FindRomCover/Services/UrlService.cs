using System.Diagnostics;

namespace FindRomCover.Services;

/// <summary>
///     Opens external web links with the operating system's default browser, with an
///     explorer.exe fallback for systems without a registered browser association.
/// </summary>
public static class UrlService
{
    /// <summary>
    ///     Tries to open the given URL in the default browser.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns><see langword="true" /> when the URL was handed to the OS; otherwise <see langword="false" />.</returns>
    public static bool TryOpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            // Common on stripped-down systems with no default browser association
            // (Win32Exception 0x800401F5 "Application not found").
            LogService.WarningTransient(ex, $"Failed to open URL with the default browser: {url}");
        }

        try
        {
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"Failed to open URL with explorer.exe fallback: {url}");
            return false;
        }
    }
}