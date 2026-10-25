using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace FindRomCover.Services;

/// <summary>
///     Avalonia tray icon replacement for the WPF WinForms NotifyIcon wrapper.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private EventHandler? _clickedHandler;
    private TrayIcon? _trayIcon;
    private TrayIcons? _trayIcons;

    /// <summary>Raised when the user requests the main window be restored from the tray.</summary>
    public event Action? RestoreRequested;

    /// <summary>Raised when the user requests the application exit from the tray menu.</summary>
    public event Action? ExitRequested;

    /// <summary>
    ///     Gets a value indicating whether a native tray implementation was created.
    ///     Callers must not hide the main window when this is <see langword="false" />.
    /// </summary>
    public bool IsAvailable => _trayIcon?.NativeMenuExporter != null;

    /// <summary>Gets or sets a value indicating whether the tray icon is visible.</summary>
    public bool Visible
    {
        get => _trayIcon?.IsVisible ?? false;
        set
        {
            if (_trayIcon != null) _trayIcon.IsVisible = value;
        }
    }

    /// <summary>Creates the tray icon with its "Show" and "Exit" menu entries and click handling.</summary>
    public void Initialize()
    {
        // Idempotent: a previous icon must be disposed before it is replaced, otherwise
        // its native handle leaks and Clicked handlers accumulate.
        DisposeCore();

        var showItem = new NativeMenuItem("Show FindRomCover");
        showItem.Click += (_, _) => RestoreRequested?.Invoke();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new NativeMenu();
        menu.Add(showItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://FindRomCover/icon/icon.png"))),
            ToolTipText = "FindRomCover",
            Menu = menu,
            IsVisible = false
        };

        _clickedHandler = (_, _) => RestoreRequested?.Invoke();
        trayIcon.Clicked += _clickedHandler;

        // Avalonia only creates the native icon when the TrayIcon is attached to the
        // application's TrayIcons collection. Without this registration IsVisible has no
        // effect and minimizing hides the window with no way to restore it.
        var application = Application.Current ??
                          throw new InvalidOperationException("The Avalonia application is not initialized.");
        var icons = TrayIcon.GetIcons(application);
        if (icons == null)
        {
            icons = new TrayIcons();
            TrayIcon.SetIcons(application, icons);
        }

        _trayIcons = icons;
        _trayIcon = trayIcon;
        icons.Add(trayIcon);
    }

    /// <summary>
    ///     Placeholder for tray balloon notifications, which Avalonia does not support cross-platform.
    /// </summary>
    /// <param name="title">The balloon title.</param>
    /// <param name="message">The balloon message.</param>
    public void ShowBalloonTip(string title, string message)
    {
        // Cross-platform tray balloons are not supported by Avalonia; the status
        // message in the main window already communicates the state.
    }

    /// <summary>Disposes the tray icon and releases it.</summary>
    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        var trayIcon = _trayIcon;
        var trayIcons = _trayIcons;
        _trayIcon = null;
        _trayIcons = null;

        if (trayIcon == null) return;

        if (_clickedHandler != null)
        {
            trayIcon.Clicked -= _clickedHandler;
            _clickedHandler = null;
        }

        // Removing the icon from the collection detaches and disposes its native
        // implementation; Dispose is idempotent and also covers a failed add.
        trayIcons?.Remove(trayIcon);
        trayIcon.Dispose();
    }
}
