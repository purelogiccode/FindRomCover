using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FindRomCover.Interfaces;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;

namespace FindRomCover;

/// <summary>
///     Avalonia application entry point that bootstraps services, the main window and global
///     exception handling.
/// </summary>
public class App : Application
{
    private static readonly Lazy<IAudioService> AudioServiceLazy = new(static () =>
    {
        try
        {
            return new LocalAudioService();
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Failed to initialize audio service, falling back to NullAudioService");
            return new NullAudioService();
        }
    });

    /// <summary>
    ///     Gets the application service provider, or <see langword="null"/> before startup completes.
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    ///     Gets the settings manager resolved from <see cref="ServiceProvider"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the service provider is not initialized.</exception>
    public static SettingsManager SettingsManager
    {
        get
        {
            if (ServiceProvider != null) return ServiceProvider.GetRequiredService<SettingsManager>();

            throw new InvalidOperationException("ServiceProvider has not been initialized.");
        }
    }

    /// <summary>
    ///     Gets the debug/log window created during startup, or <see langword="null"/> when it is closed.
    /// </summary>
    public static DebugWindow? LogWindow { get; private set; }

    /// <summary>
    ///     Gets the image folder passed on the command line, or <see langword="null"/> when none was given.
    /// </summary>
    public static string? StartupImageFolderPath { get; private set; }

    /// <summary>
    ///     Gets the ROM folder passed on the command line, or <see langword="null"/> when none was given.
    /// </summary>
    public static string? StartupRomFolderPath { get; private set; }

    /// <summary>
    ///     Gets the audio service, falling back to a silent implementation when audio is unavailable.
    /// </summary>
    public static IAudioService AudioService => AudioServiceLazy.Value;

    /// <summary>
    ///     Bootstraps logging, services, theme, exception handlers and the main window once the
    ///     Avalonia framework initialization has completed.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            try
            {
                LogService.Initialize();
                ConfigureCoreServices();
                RegisterGlobalExceptionHandlers();

                var services = new ServiceCollection();
                services.AddSingleton<SettingsManager>();
                ServiceProvider = services.BuildServiceProvider();

                FireAndForget(ApplicationStatsService.RecordStartupAsync);

                try
                {
                    ResourceLimits.Memory = AppConstants.DefaultMemoryLimit;
                    ResourceLimits.Thread = AppConstants.DefaultThreadLimit;
                }
                catch (Exception ex)
                {
                    LogService.WarningTransient(ex, "Failed to set resource limits.");
                }

                ParseStartupArguments(desktop.Args ?? []);

                LogWindow = new DebugWindow();

                var settings = SettingsManager;

                var folderToClean = !string.IsNullOrEmpty(StartupImageFolderPath)
                    ? StartupImageFolderPath
                    : settings.LastImageFolder;

                if (!string.IsNullOrEmpty(folderToClean) && Directory.Exists(folderToClean))
                    _ = Task.Run(() => ImageProcessor.CleanupOrphanedTempFiles(folderToClean));

                ApplyTheme(settings.BaseTheme, settings.AccentColor);

                var mainWindow = new MainWindow(SettingsManager, StartupImageFolderPath, StartupRomFolderPath);
                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.Exit += OnDesktopExit;

                mainWindow.Show();

                LogService.Information("Application started successfully.");

                FireAndForget(CheckForUpdatesAsync);
            }
            catch (Exception ex)
            {
                LogService.Fatal(ex, "Error in OnFrameworkInitializationCompleted");
                Dialogs.ShowMessage(null,
                    $"A fatal error occurred during application startup:\n\n{ex.Message}\n\nThe application will now close.",
                    "Fatal Startup Error", MessageIcon.Error);
                Environment.Exit(1);
            }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ParseStartupArguments(string[] args)
    {
        switch (args.Length)
        {
            case >= 2:
            {
                var imageFolderPath = args[0];
                var romFolderPath = args[1];

                if (Directory.Exists(imageFolderPath) && Directory.Exists(romFolderPath))
                {
                    StartupImageFolderPath = imageFolderPath;
                    StartupRomFolderPath = romFolderPath;
                }

                break;
            }
            case 1:
            {
                if (Directory.Exists(args[0])) StartupImageFolderPath = args[0];

                break;
            }
        }
    }

    private static void ConfigureCoreServices()
    {
        LogService.UiDispatcher = static action => Dispatcher.UIThread.Post(action);

        SettingsManager.ErrorReporter = static (message, title) =>
            Dispatcher.UIThread.Post(() => Dialogs.ShowMessage(null, message, title, MessageIcon.Error));

        ImageSources.CreateFromPngBytes = static bytes =>
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        };

        ImageSources.CreateBrokenImage = static () =>
        {
            try
            {
                using var stream = AssetLoader.Open(
                    new Uri("avares://FindRomCover/Assets/images/brokenimage.png"));
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Failed to load broken image fallback resource");
                return null;
            }
        };
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception ??
                     new InvalidOperationException(args.ExceptionObject.ToString() ?? "Unknown AppDomain exception");
            LogService.Fatal(ex, "Unhandled AppDomain exception - Application will terminate");
            Dispatcher.UIThread.Post(() => Dialogs.ShowMessage(null,
                "An unexpected error occurred and the application needs to close.\n\n" +
                "Please report this issue to the development team.",
                "Fatal Error", MessageIcon.Error));
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (IsWebViewAdapterLifecycleFailure(args.Exception))
                LogService.WarningTransient(args.Exception,
                    "Ignored a WebView2 adapter exception during initialization/teardown.");
            else
                LogService.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            if (IsWebViewAdapterLifecycleFailure(args.Exception))
            {
                // Known race inside Avalonia's WebView2 adapter, not an application bug:
                // the adapter either completes its async initialization after the window
                // closed (E_ABORT) or processes a pending size change after its controller
                // was torn down (ERROR_INVALID_STATE). Reported by issues #68251, #68252,
                // #68285 and #68286 - log as transient instead of alerting the user.
                LogService.WarningTransient(args.Exception,
                    "Ignored a WebView2 adapter exception during initialization/teardown.");
                args.Handled = true;
                return;
            }

            LogService.Error(args.Exception, "Unhandled dispatcher exception (UI Thread)");
            Dispatcher.UIThread.Post(() => Dialogs.ShowMessage(null,
                "An unexpected error occurred.\n\n" +
                $"Error: {args.Exception.Message}\n\n" +
                "The error has been reported to the development team.",
                "Error", MessageIcon.Error));
            args.Handled = true;
        };
    }

    /// <summary>
    ///     Detects the WebView2 adapter lifecycle failures reported by issues #68251, #68252,
    ///     #68285 and #68286: Avalonia's WebView2 adapter throws a COM exception (E_ABORT or
    ///     ERROR_INVALID_STATE) from its asynchronous initialization or from a pending
    ///     size-change notification after its controller was already closed. These originate
    ///     inside the framework during window startup or shutdown, so they are environmental
    ///     races instead of application bugs.
    /// </summary>
    /// <param name="exception">The exception (possibly an AggregateException) to inspect.</param>
    /// <returns>True when the failure is a known WebView2 adapter lifecycle race.</returns>
    private static bool IsWebViewAdapterLifecycleFailure(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is AggregateException aggregateException)
            {
                if (aggregateException.InnerExceptions.Any(IsWebViewAdapterLifecycleFailure)) return true;
                continue;
            }

            if (current is COMException comException &&
                (uint)comException.HResult is 0x80004004 or 0x8007139F &&
                comException.StackTrace?.Contains("WebView", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    }

    private static void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            if (AudioService is IDisposable disposableAudioService) disposableAudioService.Dispose();
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Error disposing AudioService during shutdown.");
        }

        try
        {
            HttpClientHelper.Dispose();
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Error disposing HttpClientHelper during shutdown.");
        }

        try
        {
            ErrorLogger.Dispose();
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Error disposing ErrorLogger during shutdown.");
        }

        try
        {
            if (ServiceProvider is IDisposable disposableProvider) disposableProvider.Dispose();
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Error disposing ServiceProvider during shutdown.");
        }

        try
        {
            if (LogWindow != null)
            {
                LogWindow.ForceClose();
                LogWindow.Close();
                LogWindow = null;
            }
        }
        catch (Exception ex)
        {
            Debug.Print($"Error closing LogWindow during shutdown: {ex.Message}");
        }

        try
        {
            LogService.Dispose();
        }
        catch (Exception ex)
        {
            Debug.Print($"Error disposing LogService during shutdown: {ex.Message}");
        }
    }

    private static void FireAndForget(Func<Task> asyncAction)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await asyncAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "FireAndForget caught unhandled exception");
            }
        });
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateInfo = await UpdateCheckService.CheckForUpdateAsync();
            if (updateInfo is { IsUpdateAvailable: true })
            {
                var choice = await Dialogs.ShowQuestionAsync(null,
                    "A new version of FindRomCover is available!\n\n" +
                    $"Current: {updateInfo.CurrentVersion}\n" +
                    $"Latest: {updateInfo.LatestVersion}\n\n" +
                    "Would you like to download it?",
                    "Update Available", MessageIcon.Information);

                if (choice == MessageResult.Yes)
                    UrlService.TryOpenUrl(updateInfo.ReleaseUrl);
            }
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Update check failed silently.");
        }
    }

    /// <summary>
    ///     Applies the given theme, persists it in the settings and saves the settings file.
    /// </summary>
    /// <param name="baseTheme">The base theme name (for example "Light" or "Dark").</param>
    /// <param name="accentColor">The accent color name to apply.</param>
    public static void ChangeTheme(string baseTheme, string accentColor)
    {
        try
        {
            ApplyTheme(baseTheme, accentColor);
            SettingsManager.BaseTheme = baseTheme;
            SettingsManager.AccentColor = accentColor;
            SettingsManager.SaveSettings();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ChangeTheme");
        }
    }

    private static void ApplyTheme(string baseTheme, string accentColor)
    {
        try
        {
            ThemePalette.ApplyTheme(baseTheme, accentColor);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            FireAndForget(() =>
            {
                LogService.Error(ex, $"Error applying theme: {baseTheme}.{accentColor}");
                return Task.CompletedTask;
            });
        }
    }

    /// <summary>
    ///     Shuts down the desktop application lifetime, or exits the process when no desktop
    ///     lifetime is available.
    /// </summary>
    public static void ShutdownApplication()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }

    private sealed class NullAudioService : IAudioService
    {
        public void PlayClickSound()
        {
        }

        public void Dispose()
        {
        }
    }
}