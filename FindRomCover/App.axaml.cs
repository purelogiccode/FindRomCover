using System.Diagnostics;
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

    private static WeakReference<MainWindow>? _mainWindow;

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
                _mainWindow = new WeakReference<MainWindow>(mainWindow);
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.Exit += OnDesktopExit;

                mainWindow.Show();

                LogService.Information("Application started successfully.");

                FireAndForget(CheckForUpdatesAsync);
            }
            catch (Exception ex)
            {
                WriteCrashLog("Fatal startup error", ex);

                try
                {
                    var dialogTask = Dialogs.ShowMessageAsync(null,
                        $"A fatal error occurred during application startup:\n\n{ex.Message}\n\nThe application will now close.",
                        "Fatal Startup Error", MessageIcon.Error);

                    // Do not call Environment.Exit before the dialog has a chance to paint.
                    // The dialog closes the process when the user dismisses it; the timeout
                    // guarantees exit if the UI never comes up.
                    _ = Task.WhenAny(dialogTask, Task.Delay(TimeSpan.FromSeconds(60)))
                        .ContinueWith(static _ => Environment.Exit(1), TaskScheduler.Default);
                }
                catch (Exception dialogEx)
                {
                    WriteCrashLog("Could not show the fatal startup dialog", dialogEx);
                    Environment.Exit(1);
                }
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
            WriteCrashLog("Unhandled AppDomain exception - Application will terminate", ex);
            Dispatcher.UIThread.Post(() => Dialogs.ShowMessage(null,
                "An unexpected error occurred and the application needs to close.\n\n" +
                "Please report this issue to the development team.",
                "Fatal Error", MessageIcon.Error));
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (WebViewExceptionClassifier.IsRuntimeUnavailable(args.Exception))
            {
                // A missing WebView2 runtime or loader (issues #68439, #68440) is an
                // environment problem on the user's machine, not an application bug.
                LogService.WarningTransient(args.Exception,
                    "The WebView2 runtime is not available; the embedded web search is disabled.");
                NotifyWebViewRuntimeUnavailable();
            }
            else if (WebViewExceptionClassifier.IsAdapterLifecycleFailure(args.Exception))
                LogService.WarningTransient(args.Exception,
                    "Ignored a WebView2 adapter exception during initialization/teardown.");
            else
                LogService.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            if (WebViewExceptionClassifier.IsRuntimeUnavailable(args.Exception))
            {
                // Missing WebView2 runtime/loader (issues #68439, #68440): disable the
                // embedded web search and show the runtime guidance instead of a generic
                // application error dialog.
                LogService.WarningTransient(args.Exception,
                    "The WebView2 runtime is not available; the embedded web search is disabled.");
                NotifyWebViewRuntimeUnavailable();
                args.Handled = true;
                return;
            }

            if (WebViewExceptionClassifier.IsAdapterLifecycleFailure(args.Exception))
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
    ///     Tells the main window that the embedded web views cannot be initialized because the
    ///     WebView2 runtime (or its loader) is unavailable, so it can disable web search and show
    ///     the runtime guidance once.
    /// </summary>
    private static void NotifyWebViewRuntimeUnavailable()
    {
        if (_mainWindow is null || !_mainWindow.TryGetTarget(out var mainWindow)) return;

        Dispatcher.UIThread.Post(mainWindow.NotifyWebViewRuntimeUnavailable);
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

    private static void WriteCrashLog(string context, Exception exception)
    {
        try
        {
            LogService.Fatal(exception, context);
        }
        catch (Exception logEx)
        {
            Debug.Print($"LogService.Fatal failed: {logEx.Message}");
        }

        try
        {
            AppDataPaths.EnsureBaseDirectory();
            var path = AppDataPaths.LogFilePath("startup-crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception fileEx)
        {
            Debug.Print($"Could not write the startup crash log: {fileEx.Message}");
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
            catch (ObjectDisposedException ex)
            {
                // The shared HttpClient or another service was disposed during shutdown.
                LogService.Debug($"FireAndForget aborted during shutdown: {ex.Message}");
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