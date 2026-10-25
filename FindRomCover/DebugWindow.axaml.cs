using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover;

/// <summary>
///     Log viewer window that shows the live application log and lets the user copy or clear it.
/// </summary>
public partial class DebugWindow : Window
{
    private readonly ObservableCollection<LogEntry> _logMessages;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isForceClosing;
    private bool _logTextDirty;

    /// <summary>Raised when the window is hidden instead of closed (X button).</summary>
    public event Action? Hidden;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DebugWindow"/> class and populates it with
    ///     the current log messages.
    /// </summary>
    public DebugWindow()
    {
        InitializeComponent();

        _logMessages = LogService.GetLogMessages();

        // Populate the TextBox with existing logs on startup
        var initialLogText = new StringBuilder();
        foreach (var logEntry in _logMessages) initialLogText.AppendLine(logEntry.Message);

        LogTextBox.Text = initialLogText.ToString();
        ScrollLogToEnd();

        LogTextBox.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.Property.Name, "SelectedText", StringComparison.Ordinal))
                LogTextBox_SelectionChanged();
        };

        // Subscribe to collection changes to append new logs
        if (_logMessages is INotifyCollectionChanged incc) incc.CollectionChanged += LogMessages_CollectionChanged;

        // Rebuilding the whole text box for every log line froze the UI once the 5000-entry
        // cap was reached. Coalesce updates and skip them entirely while the window is hidden.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            if (_logTextDirty && IsVisible) RebuildLogText();
        };
        _refreshTimer.Start();

        Closing += OnLogWindowClosing;
        Closed += OnLogWindowClosed;
        LogService.Information("Log window initialized.");
    }

    /// <summary>
    ///     Marks the window so that the next close request is honored instead of being hidden.
    /// </summary>
    public void ForceClose()
    {
        _isForceClosing = true;
    }

    private void OnLogWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isForceClosing)
            // Allow the window to close if the application is shutting down.
            return;

        // Instead of closing, just hide the window. The main window can re-show it.
        e.Cancel = true;
        Hide();
        Hidden?.Invoke();
        LogService.Information("Log window hidden.");
    }

    private void OnLogWindowClosed(object? sender, EventArgs e)
    {
        // Unsubscribe from CollectionChanged to prevent memory leak
        if (_logMessages is INotifyCollectionChanged incc) incc.CollectionChanged -= LogMessages_CollectionChanged;

        _refreshTimer.Stop();
        LogService.Information("Log window closed and event handlers unsubscribed.");
    }

    /// <summary>Marks the log text as stale and refreshes it immediately when visible.</summary>
    public void RefreshLog()
    {
        _logTextDirty = true;
        if (IsVisible) RebuildLogText();
    }

    private void LogMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Coalesce all collection changes into the timer tick. This keeps a hidden log
        // window free of work and avoids rebuilding the full text for every new line.
        _logTextDirty = true;
    }

    private void RebuildLogText()
    {
        if (!IsVisible) return;

        // Never yank the text out from under an active selection; the next tick after
        // the user clears it will apply the pending updates.
        if (!string.IsNullOrEmpty(LogTextBox.SelectedText)) return;

        _logTextDirty = false;
        var text = new StringBuilder();
        foreach (var logEntry in _logMessages) text.AppendLine(logEntry.Message);

        LogTextBox.Text = text.ToString();
        ScrollLogToEnd();
    }

    private void ScrollLogToEnd()
    {
        LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
    }

    private void CopyLog_Click(object? sender, RoutedEventArgs e)
    {
        _ = CopyLogAsync();
    }

    private async Task CopyLogAsync()
    {
        LogService.Information("Copy All button clicked.");
        try
        {
            if (LogTextBox.Text is { Length: > 0 } text)
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                    LogService.Information("Entire log content copied to clipboard.");
                }
            }
            else
            {
                LogService.Information("Log is empty, nothing to copy.");
            }
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Failed to copy log to clipboard.");
        }
    }

    private void ClearLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            LogService.Information("Clear button clicked. Clearing log messages from view.");
            // This will trigger the CollectionChanged event with a 'Reset' action,
            // which will then clear the TextBox.
            _logMessages.Clear();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ClearLog_Click");
        }
    }

    private void CopySelection_Click(object? sender, RoutedEventArgs e)
    {
        _ = CopySelectionAsync();
    }

    private async Task CopySelectionAsync()
    {
        if (string.IsNullOrEmpty(LogTextBox.SelectedText)) return;

        LogService.Information("Copy Selection clicked.");
        try
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(LogTextBox.SelectedText);
                LogService.Information("Selected log text copied to clipboard.");
            }
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Failed to copy selection to clipboard.");
        }
    }

    private void LogTextBox_SelectionChanged()
    {
        try
        {
            var hasSelection = !string.IsNullOrEmpty(LogTextBox.SelectedText);
            CopySelectionButton.IsEnabled = hasSelection;
            ContextMenuCopySelection.IsEnabled = hasSelection;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in LogTextBox_SelectionChanged");
        }
    }
}
