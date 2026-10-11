namespace FindRomCover.Services;

/// <summary>
///     Locates an existing cover image for a ROM name by probing the recognized image
///     extensions in a folder.
/// </summary>
public static class CoverFileResolver
{
    private static readonly string[] RecognizedImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".avif",
        ".heic", ".heif", ".ico", ".svg", ".jxl", ".jp2"
    ];

    /// <summary>
    ///     Finds the cover image for a ROM by checking the sanitized and original name
    ///     against every recognized image extension.
    /// </summary>
    /// <param name="imageFolderPath">The folder to search for a cover.</param>
    /// <param name="romNameWithoutExtension">The ROM name (without extension) to look up.</param>
    /// <returns>The full path of the matching image, or null when no cover exists.</returns>
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