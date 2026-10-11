using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover;

/// <summary>
///     Log viewer window that shows the live application log and lets the user copy or clear it.
/// </summary>
public partial class DebugWindow : Window
{
    private readonly ObservableCollection<LogEntry> _logMessages;
    private bool _isForceClosing;

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
        LogService.Information("Log window hidden.");
    }

    private void OnLogWindowClosed(object? sender, EventArgs e)
    {
        // Unsubscribe from CollectionChanged to prevent memory leak
        if (_logMessages is INotifyCollectionChanged incc) incc.CollectionChanged -= LogMessages_CollectionChanged;

        LogService.Information("Log window closed and event handlers unsubscribed.");
    }

    private void LogMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            // When a new log is added to the source collection...
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
            {
                var newText = new StringBuilder();
                foreach (LogEntry item in e.NewItems) newText.AppendLine(item.Message);

                // ...append it to our TextBox.
                LogTextBox.Text += newText.ToString();
                ScrollLogToEnd();
                break;
            }
            // When the source collection is cleared...
            case NotifyCollectionChangedAction.Reset:
                // ...clear our TextBox.
                LogTextBox.Clear();
                break;
            // When the source collection trims old entries (LogService keeps the last
            // 5000), the TextBox must drop them too or it would grow without bound.
            case NotifyCollectionChangedAction.Remove:
                RebuildLogText();
                break;
        }
    }

    private void RebuildLogText()
    {
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
        LogService.Information("Copy All button clicked.");
        try
        {
            if (LogTextBox.Text is { Length: > 0 } text)
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null) _ = clipboard.SetTextAsync(text);
                LogService.Information("Entire log content copied to clipboard.");
            }
            else
            {
                LogService.Information("Log is empty, nothing to copy.");
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to copy log to clipboard.");
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
        if (string.IsNullOrEmpty(LogTextBox.SelectedText)) return;

        LogService.Information("Copy Selection clicked.");
        try
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null) _ = clipboard.SetTextAsync(LogTextBox.SelectedText);
            LogService.Information("Selected log text copied to clipboard.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to copy selection to clipboard.");
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
