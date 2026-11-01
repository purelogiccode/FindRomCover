using System.Collections.Concurrent;

namespace FindRomCover.Services;

/// <summary>
///     Locates an existing cover image for a ROM name by probing the recognized image
///     extensions in a folder.
/// </summary>
public static class CoverFileResolver
{
    private static readonly ConcurrentDictionary<string, CachedDirectory> DirectoryCache =
        new(AppConstants.FilePathComparer);

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);
    private const int MaxCacheEntries = 64;

    private static readonly string[] RecognizedImageExtensions =
    [
        ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".jpe", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".avif",
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

        if (OperatingSystem.IsWindows())
        {
            foreach (var baseName in baseNames)
                foreach (var extension in RecognizedImageExtensions)
                {
                    var imagePath = Path.Combine(imageFolderPath, baseName + extension);
                    if (File.Exists(imagePath)) return imagePath;
                }

            return null;
        }

        // Linux and macOS file systems are case-sensitive. Probe a cached directory
        // listing case-insensitively so a cover saved as "Sonic 1.PNG" is still found
        // for the ROM "Sonic 1".
        var files = GetDirectoryFiles(imageFolderPath);
        foreach (var baseName in baseNames)
            foreach (var extension in RecognizedImageExtensions)
                if (files.TryGetValue(baseName + extension, out var imagePath))
                    return imagePath;

        return null;
    }

    private static Dictionary<string, string> GetDirectoryFiles(string imageFolderPath)
    {
        string cacheKey;
        try
        {
            cacheKey = Path.GetFullPath(imageFolderPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            cacheKey = imageFolderPath;
        }

        var now = DateTime.UtcNow;
        var lastWriteUtc = GetDirectoryLastWriteUtc(imageFolderPath);

        // The 5-second window alone could hide a cover saved by the app itself; the
        // directory timestamp invalidates the listing as soon as a file is added/removed.
        if (DirectoryCache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresUtc > now &&
            cached.LastWriteUtc == lastWriteUtc)
            return cached.Files;

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.EnumerateFiles(imageFolderPath))
                files[Path.GetFileName(path)] = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            // A momentary lock (AV/sync) must not be cached as "folder is empty":
            // that would make an existing cover look missing for the next 5 seconds.
            LogService.WarningTransient(ex, $"Could not enumerate the image folder '{imageFolderPath}'.");
            return files;
        }

        DirectoryCache[cacheKey] = new CachedDirectory(now.Add(CacheLifetime), lastWriteUtc, files);
        PruneCache(now);
        return files;
    }

    private static void PruneCache(DateTime now)
    {
        foreach (var pair in DirectoryCache)
            if (pair.Value.ExpiresUtc <= now)
                DirectoryCache.TryRemove(pair.Key, out _);

        // Bound the cache: switching between many folders must not grow the process
        // forever. Clearing is cheap and only happens after 64 distinct folders.
        if (DirectoryCache.Count > MaxCacheEntries) DirectoryCache.Clear();
    }

    private static DateTime GetDirectoryLastWriteUtc(string imageFolderPath)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(imageFolderPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            return DateTime.MinValue;
        }
    }

    private sealed record CachedDirectory(DateTime ExpiresUtc, DateTime LastWriteUtc,
        Dictionary<string, string> Files);
}
