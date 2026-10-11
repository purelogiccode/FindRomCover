using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FindRomCover.Models;

namespace FindRomCover.Services;

/// <summary>
///     Cross-platform replacement for WPF MessageBox with the same look as the
///     application: themed window, icon glyph and Yes/No or OK buttons.
/// </summary>
public static class Dialogs
{
    private const string InfoGlyph = "\u2139";
    private const string WarningGlyph = "\u26A0";
    private const string ErrorGlyph = "\u2716";
    private const string QuestionGlyph = "?";

    /// <summary>
    ///     Shows a message dialog with an OK button without waiting for it to close.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or <see langword="null" />.</param>
    /// <param name="message">The message body.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="icon">The icon to display.</param>
    public static void ShowMessage(Window? owner, string message, string title,
        MessageIcon icon = MessageIcon.Information)
    {
        _ = ShowMessageAsync(owner, message, title, icon);
    }

    /// <summary>
    ///     Shows a modal message dialog with an OK button.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or <see langword="null" />.</param>
    /// <param name="message">The message body.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="icon">The icon to display.</param>
    /// <returns>The button the user pressed.</returns>
    public static Task<MessageResult> ShowMessageAsync(Window? owner, string message, string title,
        MessageIcon icon = MessageIcon.Information)
    {
        return ShowCoreAsync(owner, message, title, icon, [("OK", MessageResult.Ok)]);
    }

    /// <summary>
    ///     Shows a modal question dialog with Yes and No buttons.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or <see langword="null" />.</param>
    /// <param name="message">The message body.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="icon">The icon to display.</param>
    /// <returns><see cref="MessageResult.Yes" /> or <see cref="MessageResult.No" />.</returns>
    public static Task<MessageResult> ShowQuestionAsync(Window? owner, string message, string title,
        MessageIcon icon = MessageIcon.Warning)
    {
        return ShowCoreAsync(owner, message, title, icon,
            [("Yes", MessageResult.Yes), ("No", MessageResult.No)]);
    }

    /// <summary>
    ///     Shows a modal input dialog with a single text box.
    /// </summary>
    /// <param name="owner">The window that owns the dialog, or <see langword="null" />.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="prompt">The placeholder text shown in the text box.</param>
    /// <returns>The entered text, or <see langword="null" /> when the dialog was cancelled.</returns>
    public static async Task<string?> ShowInputAsync(Window? owner, string title, string prompt)
    {
        return await OnUiAsync(async () =>
        {
            var textBox = new TextBox
            {
                PlaceholderText = prompt,
                Margin = new Thickness(16, 8, 16, 8),
                MinWidth = 360
            };

            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.Height,
                Width = 420,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen
            };

            string? result = null;

            var okButton = new Button { Content = "OK", MinWidth = 84, IsDefault = true };
            okButton.Click += (_, _) =>
            {
                result = textBox.Text;
                dialog.Close(result);
            };

            var cancelButton = new Button { Content = "Cancel", MinWidth = 84, IsCancel = true };
            cancelButton.Click += (_, _) => dialog.Close(null);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 8, 16, 16)
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 16, 16, 0)
            });
            panel.Children.Add(textBox);
            panel.Children.Add(buttons);
            dialog.Content = panel;

            return await ShowOwnedAsync<string?>(dialog, owner, null);
        });
    }

    private static async Task<MessageResult> ShowCoreAsync(Window? owner, string message, string title,
        MessageIcon icon, (string Text, MessageResult Result)[] buttons)
    {
        return await OnUiAsync(async () =>
        {
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.Height,
                Width = 460,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
                Icon = owner?.Icon
            };

            var iconText = new TextBlock
            {
                Text = GlyphFor(icon),
                FontSize = 26,
                Margin = new Thickness(16, 14, 0, 16),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = new SolidColorBrush(ColorFor(icon))
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 16, 16, 16),
                VerticalAlignment = VerticalAlignment.Center,
                MinHeight = 40
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var isFirst = true;
            foreach (var (text, result) in buttons)
            {
                var button = new Button
                {
                    Content = text,
                    MinWidth = 84,
                    IsDefault = isFirst,
                    IsCancel = result is MessageResult.No or MessageResult.Cancel
                };
                var capturedResult = result;
                button.Click += (_, _) => dialog.Close(capturedResult);
                buttonPanel.Children.Add(button);
                isFirst = false;
            }

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("*"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*")
            };
            Grid.SetColumn(iconText, 0);
            Grid.SetColumn(messageText, 1);
            grid.Children.Add(iconText);
            grid.Children.Add(messageText);

            var root = new DockPanel();
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            root.Children.Add(buttonPanel);
            root.Children.Add(grid);
            dialog.Content = root;

            return await ShowOwnedAsync(dialog, owner, MessageResult.None);
        });
    }

    private static async Task<T> ShowOwnedAsync<T>(Window dialog, Window? owner, T fallback)
    {
        var actualOwner = owner ?? GetMainWindow();
        if (actualOwner != null && actualOwner != dialog)
            return await dialog.ShowDialog<T>(actualOwner);

        var tcs = new TaskCompletionSource<T>();
        dialog.Closed += (_, _) => tcs.TrySetResult(fallback);
        dialog.Show();
        return await tcs.Task;
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private static string GlyphFor(MessageIcon icon)
    {
        return icon switch
        {
            MessageIcon.Information => InfoGlyph,
            MessageIcon.Warning => WarningGlyph,
            MessageIcon.Error => ErrorGlyph,
            MessageIcon.Question => QuestionGlyph,
            _ => string.Empty
        };
    }

    private static Color ColorFor(MessageIcon icon)
    {
        return icon switch
        {
            MessageIcon.Information => Color.Parse("#FF0078D7"),
            MessageIcon.Warning => Color.Parse("#FFF0A30A"),
            MessageIcon.Error => Color.Parse("#FFE51400"),
            MessageIcon.Question => Color.Parse("#FF0078D7"),
            _ => Color.Parse("#FF808080")
        };
    }

    private static async Task<T> OnUiAsync<T>(Func<Task<T>> func)
    {
        if (Dispatcher.UIThread.CheckAccess()) return await func();

        return await Dispatcher.UIThread.InvokeAsync(func);
    }
}
