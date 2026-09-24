using System.IO;

namespace FindRomCover.Services;

public static class CoverFileResolver
{
    private static readonly string[] RecognizedImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".avif",
        ".heic", ".heif", ".ico", ".svg", ".jxl", ".jp2"
    ];

    public static string? FindCover(string imageFolderPath, string romNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(imageFolderPath) || string.IsNullOrWhiteSpace(romNameWithoutExtension))
            return null;

        var sanitized = SearchQueryHelper.SanitizeFileName(romNameWithoutExtension);
        // Prefer the sanitized base name: that is the canonical target used by every
        // writer, so it wins when both variants exist.
        var baseNames = string.Equals(sanitized, romNameWithoutExtension, StringComparison.Ordinal)
            ? [romNameWithoutExtension]
            : new[] { sanitized, romNameWithoutExtension };

        foreach (var baseName in baseNames)
        {
            foreach (var extension in RecognizedImageExtensions)
            {
                var imagePath = Path.Combine(imageFolderPath, baseName + extension);
                if (File.Exists(imagePath)) return imagePath;
            }
        }

        return null;
    }
}
