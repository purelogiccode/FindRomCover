using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace FindRomCover.Services;

/// <summary>
///     Cross-platform replacement for the Windows-only screen capture service.
///     Renders the application window to a PNG file. Screenshots are stored in a
///     "Screenshot" folder next to the application; when that folder is not writable
///     (for example a Program Files installation) the per-user application data
///     folder is used instead.
/// </summary>
public static class ScreenshotService
{
    private const string ScreenshotFolderName = "Screenshot";

    /// <summary>
    ///     Captures the given window and saves it as a PNG file.
    /// </summary>
    /// <param name="window">The window to capture.</param>
    /// <returns>The full path of the saved file, or <see langword="null" /> when the capture failed.</returns>
    public static string? CaptureWindow(Window window)
    {
        try
        {
            var width = (int)Math.Ceiling(window.Bounds.Width);
            var height = (int)Math.Ceiling(window.Bounds.Height);

            if (width <= 0 || height <= 0)
            {
                LogService.Warning("Screenshot: window has no size.");
                return null;
            }

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(window);

            return SaveScreenshot(
                filePath => bitmap.Save(filePath, new PngBitmapEncoderOptions()),
                DateTime.Now);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Screenshot: capture failed.");
            return null;
        }
    }

    /// <summary>
    ///     Gets the folders that are tried in order when saving a screenshot. The first
    ///     entry is the "Screenshot" folder next to the application, the second is the
    ///     per-user application data fallback.
    /// </summary>
    /// <returns>The candidate folders, in priority order.</returns>
    internal static IReadOnlyList<string> GetCandidateFolders()
    {
        return
        [
            Path.Combine(AppContext.BaseDirectory, ScreenshotFolderName),
            Path.Combine(AppDataPaths.BaseDirectory, ScreenshotFolderName)
        ];
    }

    /// <summary>
    ///     Saves a screenshot using the supplied write delegate, falling back to the
    ///     per-user application data folder when the application folder is not writable.
    /// </summary>
    /// <param name="writeFile">Delegate that writes the PNG data to the given path.</param>
    /// <param name="timestamp">Timestamp used to build the unique file name.</param>
    /// <param name="folders">Overrides the candidate folders (used by tests).</param>
    /// <returns>The path of the saved file, or <see langword="null" /> when every folder failed.</returns>
    internal static string? SaveScreenshot(Action<string> writeFile, DateTime timestamp,
        IReadOnlyList<string>? folders = null)
    {
        ArgumentNullException.ThrowIfNull(writeFile);

        var fileName = $"Screenshot_{timestamp.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)}.png";

        foreach (var folder in folders ?? GetCandidateFolders())
        {
            var filePath = GetUniqueFilePath(folder, fileName);

            try
            {
                Directory.CreateDirectory(folder);
                writeFile(filePath);

                LogService.Information($"Screenshot saved: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, $"Screenshot: failed to save inside '{folder}', trying next location.");
            }
        }

        LogService.Error("Screenshot: could not be saved to any candidate folder.");
        return null;
    }

    /// <summary>
    ///     Builds a file path that does not collide with an existing file. Two captures
    ///     within the same millisecond would otherwise silently overwrite each other.
    /// </summary>
    /// <param name="folder">The target folder.</param>
    /// <param name="fileName">The preferred file name.</param>
    /// <returns>A file path that does not exist yet, or the preferred name after 1000 attempts.</returns>
    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var filePath = Path.Combine(folder, fileName);
        if (!File.Exists(filePath)) return filePath;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var i = 1; i <= 1000; i++)
        {
            filePath = Path.Combine(folder, $"{name}_{i}{extension}");
            if (!File.Exists(filePath)) return filePath;
        }

        return Path.Combine(folder, $"{name}_{Guid.NewGuid():N}{extension}");
    }
}