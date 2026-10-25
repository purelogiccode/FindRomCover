using System.Diagnostics.CodeAnalysis;
using Avalonia;

namespace FindRomCover;

/// <summary>
///     Application entry point that boots the Avalonia desktop lifetime.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Application entry point that starts the Avalonia desktop lifetime.
    /// </summary>
    /// <param name="args">The command line arguments passed to the application.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    ///     Creates the configured Avalonia application builder.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/> instance.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global",
        Justification = "The Avalonia designer requires a public BuildAvaloniaApp method.")]
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
