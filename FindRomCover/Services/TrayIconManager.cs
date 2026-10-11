using Avalonia.Controls;
using Avalonia.Platform;

namespace FindRomCover.Services;

/// <summary>
///     Avalonia tray icon replacement for the WPF WinForms NotifyIcon wrapper.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private TrayIcon? _trayIcon;

    /// <summary>Raised when the user requests the main window be restored from the tray.</summary>
    public event Action? RestoreRequested;
    /// <summary>Raised when the user requests the application exit from the tray menu.</summary>
    public event Action? ExitRequested;

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
        var showItem = new NativeMenuItem("Show FindRomCover");
        showItem.Click += (_, _) => RestoreRequested?.Invoke();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new NativeMenu();
        menu.Add(showItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://FindRomCover/Assets/images/logo.png"))),
            ToolTipText = "FindRomCover",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => RestoreRequested?.Invoke();
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
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
