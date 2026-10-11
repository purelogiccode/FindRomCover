using System.Windows;
using System.Windows.Navigation;
using FindRomCover.Managers;
using FindRomCover.Services;

namespace FindRomCover;

public partial class ApiSettingsWindow
{
    private readonly SettingsManager _settingsManager;

    public ApiSettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            TxtGoogleKey.Text = _settingsManager.GoogleKey;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in LoadSettings");
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsManager.GoogleKey = TxtGoogleKey.Text.Trim();

            _settingsManager.SaveSettings();
            LogService.Information("API settings saved successfully.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("There was an error saving the settings.\n\n" +
                            "The developer will try to fix this.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            LogService.Error(ex, "Error saving API settings.");
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // TryOpenUrl logs failures as warnings; the URL is shown so the user can copy it.
        if (!UrlService.TryOpenUrl(e.Uri.AbsoluteUri))
            MessageBox.Show("Could not open the link. Please copy and paste the URL into your browser.", "Link Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        e.Handled = true;
    }
}