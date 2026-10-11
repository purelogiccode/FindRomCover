using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace FindRomCover;

/// <summary>
///     MahApps-compatible theme palette: maps the WPF application's theme/accent
///     choices onto Avalonia theme variants and accent brush resources.
/// </summary>
public static class ThemePalette
{
    private static readonly Dictionary<string, string> AccentColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Red"] = "#E51400",
        ["Green"] = "#60A917",
        ["Blue"] = "#0078D7",
        ["Purple"] = "#6459DF",
        ["Orange"] = "#FA6800",
        ["Lime"] = "#A4C400",
        ["Emerald"] = "#008A00",
        ["Teal"] = "#00ABA9",
        ["Cyan"] = "#1BA1E2",
        ["Cobalt"] = "#0050EF",
        ["Indigo"] = "#6A00FF",
        ["Violet"] = "#AA00FF",
        ["Pink"] = "#F472D0",
        ["Magenta"] = "#D80073",
        ["Crimson"] = "#A20025",
        ["Amber"] = "#F0A30A",
        ["Yellow"] = "#FEDE06",
        ["Brown"] = "#825A2C",
        ["Olive"] = "#6D8764",
        ["Steel"] = "#647687",
        ["Mauve"] = "#76608A",
        ["Taupe"] = "#87794E",
        ["Sienna"] = "#A0522D"
    };

    /// <summary>
    ///     Applies the given base theme and accent color to the application,
    ///     updating both the custom accent brushes and the Fluent theme accent resources.
    /// </summary>
    /// <param name="baseTheme">Name of the base theme; "Dark" selects the dark variant, any other value the light variant.</param>
    /// <param name="accentColor">Name of the accent color to apply; unknown names fall back to blue.</param>
    public static void ApplyTheme(string baseTheme, string accentColor)
    {
        if (Application.Current is not { } app) return;

        app.RequestedThemeVariant = string.Equals(baseTheme, "Dark", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        var hex = AccentColors.GetValueOrDefault(accentColor, "#0078D7");
        var accent = Color.Parse(hex);
        var accentAlpha = Color.FromArgb(0xCC, accent.R, accent.G, accent.B);

        app.Resources["AccentColor"] = accent;
        app.Resources["AccentBrush"] = new SolidColorBrush(accentAlpha);
        app.Resources["Accent2Brush"] = new SolidColorBrush(Color.FromArgb(0x99, accent.R, accent.G, accent.B));
        app.Resources["Accent3Brush"] = new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B));
        app.Resources["Accent4Brush"] = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
        app.Resources["AccentSolidBrush"] = new SolidColorBrush(accent);

        // Mirror the accent onto Fluent theme system resources so standard controls
        // (tabs, focus rings, selection highlights) pick up the same color.
        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorLight1"] = Color.FromArgb(0xFF,
            (byte)Math.Min(255, accent.R + 20), (byte)Math.Min(255, accent.G + 20),
            (byte)Math.Min(255, accent.B + 20));
        app.Resources["SystemAccentColorDark1"] = Color.FromArgb(0xFF,
            (byte)Math.Max(0, accent.R - 20), (byte)Math.Max(0, accent.G - 20),
            (byte)Math.Max(0, accent.B - 20));
        app.Resources["SystemControlHighlightAccentBrush"] = new SolidColorBrush(accent);
        app.Resources["SystemControlBackgroundAccentBrush"] = new SolidColorBrush(accent);
        app.Resources["SystemControlForegroundAccentBrush"] = new SolidColorBrush(accent);
        app.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accent);
        app.Resources["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(accent);
    }
}
