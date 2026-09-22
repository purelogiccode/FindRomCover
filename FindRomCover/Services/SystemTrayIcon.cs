using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FindRomCover.Services;

public sealed class SystemTrayIcon : IDisposable
{
    private bool _disposed;
    private NotifyIcon? _notifyIcon;

    public bool Visible
    {
        get => _notifyIcon?.Visible ?? false;
        set => _notifyIcon?.Visible = value;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_notifyIcon != null)
        {
            var contextMenu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
            contextMenu?.Dispose();
        }
    }

    public event Action? RestoreRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        Icon icon;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("FindRomCover.icon.icon.ico");
            icon = stream != null ? new Icon(stream) : SystemIcons.Application;
        }
        catch
        {
            icon = SystemIcons.Application;
        }

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Restore", null, (_, _) => RestoreRequested?.Invoke());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "FindRomCover",
            Visible = false,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreRequested?.Invoke();
    }

    public void ShowBalloonTip(string title, string text)
    {
        _notifyIcon?.ShowBalloonTip(1000, title, text, ToolTipIcon.Info);
    }
}