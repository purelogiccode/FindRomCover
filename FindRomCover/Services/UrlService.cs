using System.Diagnostics;

namespace FindRomCover.Services;

public static class UrlService
{
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
            LogService.Warning(ex, $"Failed to open URL with the default browser: {url}");
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
            LogService.Warning(ex, $"Failed to open URL with explorer.exe fallback: {url}");
            return false;
        }
    }
}
