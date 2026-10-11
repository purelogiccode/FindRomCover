using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using FindRomCover.Services;

namespace FindRomCover;

public partial class AboutWindow
{
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
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

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!UrlService.TryOpenUrl(e.Uri.AbsoluteUri))
            MessageBox.Show("Unable to open the link.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }
}