using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover;

/// <summary>
///     About dialog that shows the application version and links to the project pages.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AboutWindow"/> class and displays
    ///     the running application version.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
        AppVersionTextBlock.Text = ApplicationVersion;
    }

    private static string ApplicationVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return "Version: " + (version?.ToString() ?? "Unknown");
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in CloseButton_Click");
        }
    }

    private void Hyperlink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton { Tag: string url }) return;

        if (!UrlService.TryOpenUrl(url))
            Dialogs.ShowMessage(this, "Unable to open the link.", "Error", MessageIcon.Error);
    }
}