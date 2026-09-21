using System.IO;

namespace FindRomCover.Services;

public static class CoverFileResolver
{
    private static readonly string[] RecognizedImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".avif"
    ];

    public static string? FindCover(string imageFolderPath, string romNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(imageFolderPath) || string.IsNullOrWhiteSpace(romNameWithoutExtension))
            return null;

        var sanitized = SearchQueryHelper.SanitizeFileName(romNameWithoutExtension);
        var baseNames = string.Equals(sanitized, romNameWithoutExtension, StringComparison.Ordinal)
            ? [romNameWithoutExtension]
            : new[] { romNameWithoutExtension, sanitized };

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
