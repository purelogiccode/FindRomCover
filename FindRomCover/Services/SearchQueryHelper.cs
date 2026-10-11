using System.Text.RegularExpressions;

namespace FindRomCover.Services;

/// <summary>
///     Cleans ROM file names into search queries and sanitizes them for use as file
///     names on disk.
/// </summary>
internal static partial class SearchQueryHelper
{
    // Matches common patterns in parentheses or square brackets.
    // e.g., (USA), (Europe), (Japan), (Brazil), (En,Ja), [!], (Rev A), (v1.1), (Unl), (Mega Drive 4)
    private static readonly Regex TagPattern = MyRegex();

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    ///     Removes region/version tags in parentheses, brackets and braces from a file name.
    /// </summary>
    /// <param name="fileName">The raw ROM file name.</param>
    /// <returns>The cleaned query, or the original name when cleaning removed everything.</returns>
    internal static string CleanSearchQuery(string fileName)
    {
        var cleanedName = TagPattern.Replace(fileName, "").Trim();

        // If cleaning removed everything (unlikely), fall back to the original name
        return string.IsNullOrWhiteSpace(cleanedName) ? fileName : cleanedName;
    }

    /// <summary>
    ///     Makes a string safe to use as a file name on the current platform, avoiding
    ///     reserved device names and path traversal sequences.
    /// </summary>
    /// <param name="fileName">The desired file name.</param>
    /// <returns>A sanitized file name; "unnamed" when nothing usable remains.</returns>
    internal static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = fileName;
        while (sanitized.Contains("..", StringComparison.OrdinalIgnoreCase)) sanitized = sanitized.Replace("..", "");

        sanitized = new string(sanitized
            .Replace("/", "")
            .Replace("\\", "")
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());

        sanitized = sanitized.Trim().TrimEnd('.');

        if (string.IsNullOrWhiteSpace(sanitized) ||
            sanitized.All(static c => c == '.' || char.IsWhiteSpace(c)))
            return "unnamed";

        var baseName = sanitized.Split('.', 2)[0];
        if (ReservedDeviceNames.Contains(baseName))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    [GeneratedRegex(@"\s*(\(.*?\)|\[.*?\]|\{.*?\})", RegexOptions.None | RegexOptions.ExplicitCapture, 500)]
    private static partial Regex MyRegex();
}