using Avalonia.Controls;
using Avalonia.Interactivity;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover;

/// <summary>
///     Window for configuring the Google API key used by the API cover search.
/// </summary>
public partial class ApiSettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApiSettingsWindow"/> class.
    /// </summary>
    /// <param name="settingsManager">The settings manager whose Google API key is edited.</param>
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

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _settingsManager.GoogleKey = TxtGoogleKey.Text?.Trim() ?? string.Empty;

            _settingsManager.SaveSettings();
            LogService.Information("API settings saved successfully.");
            Close(true);
        }
        catch (Exception ex)
        {
            Dialogs.ShowMessage(this, "There was an error saving the settings.\n\n" +
                                      "The developer will try to fix this.", "Error", MessageIcon.Error);
            LogService.Error(ex, "Error saving API settings.");
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Hyperlink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton { Tag: string url }) return;

        // TryOpenUrl logs failures as warnings; the URL is shown so the user can copy it.
        if (!UrlService.TryOpenUrl(url))
            Dialogs.ShowMessage(this,
                "Could not open the link. Please copy and paste the URL into your browser.", "Link Error",
                MessageIcon.Warning);
    }
}
