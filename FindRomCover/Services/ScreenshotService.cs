using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace FindRomCover.Services;

/// <summary>
///     Cross-platform replacement for the Windows-only screen capture service.
///     Renders the application window to a PNG file.
/// </summary>
public static class ScreenshotService
{
    private static readonly string ScreenshotFolder = Path.Combine(AppDataPaths.BaseDirectory, "Screenshot");

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

            Directory.CreateDirectory(ScreenshotFolder);
            var filePath = Path.Combine(ScreenshotFolder, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            bitmap.Save(filePath, new PngBitmapEncoderOptions());

            LogService.Information($"Screenshot saved: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Screenshot: capture failed.");
            return null;
        }
    }
}
