using Avalonia.Controls;
using Avalonia.Platform;

namespace FindRomCover.Services;

/// <summary>
///     Avalonia tray icon replacement for the WPF WinForms NotifyIcon wrapper.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private TrayIcon? _trayIcon;

    public event Action? RestoreRequested;
    public event Action? ExitRequested;

    public bool Visible
    {
        get => _trayIcon?.IsVisible ?? false;
        set
        {
            if (_trayIcon != null) _trayIcon.IsVisible = value;
        }
    }

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

    public void ShowBalloonTip(string title, string message)
    {
        // Cross-platform tray balloons are not supported by Avalonia; the status
        // message in the main window already communicates the state.
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
