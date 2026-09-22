using System.IO;
using System.Text.RegularExpressions;

namespace FindRomCover.Services;

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

    internal static string CleanSearchQuery(string fileName)
    {
        var cleanedName = TagPattern.Replace(fileName, "").Trim();

        // If cleaning removed everything (unlikely), fall back to the original name
        return string.IsNullOrWhiteSpace(cleanedName) ? fileName : cleanedName;
    }

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

        if (string.IsNullOrWhiteSpace(sanitized))
            return "unnamed";

        var baseName = sanitized.Split('.', 2)[0];
        if (ReservedDeviceNames.Contains(baseName))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    [GeneratedRegex(@"\s*(\(.*?\)|\[.*?\]|\{.*?\})", RegexOptions.None | RegexOptions.ExplicitCapture, 500)]
    private static partial Regex MyRegex();
}