using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using FindRomCover.Managers;
using FindRomCover.Services;

namespace FindRomCover;

public partial class AiSettingsWindow
{
    private readonly SettingsManager _settingsManager;
    private bool _loading;

    public AiSettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        LoadSettings();
    }

    private string SelectedProvider => CmbProvider.SelectedItem as string ?? AppConstants.AiProviders.OpenRouter;

    private void LoadSettings()
    {
        try
        {
            _loading = true;

            ChkAiEnabled.IsChecked = _settingsManager.AiAssistEnabled;
            CmbProvider.ItemsSource = AppConstants.AiProviders.All;
            CmbProvider.SelectedItem = AppConstants.AiProviders.All.FirstOrDefault(provider =>
                string.Equals(provider, _settingsManager.AiProvider, StringComparison.OrdinalIgnoreCase));
            if (CmbProvider.SelectedItem == null) CmbProvider.SelectedItem = AppConstants.AiProviders.OpenRouter;

            TxtBaseUrl.Text = _settingsManager.AiBaseUrl;
            TxtApiKey.Text = _settingsManager.AiApiKey;
            TxtModel.Text = _settingsManager.AiModel;
            TxtTimeout.Text = _settingsManager.AiTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            TxtMaxCandidates.Text = _settingsManager.AiMaxCandidates.ToString(CultureInfo.InvariantCulture);
            TxtImageMaxDimension.Text = _settingsManager.AiImageMaxDimension.ToString(CultureInfo.InvariantCulture);
            TxtAutoSaveThreshold.Text = _settingsManager.AiAutoSaveThreshold.ToString(CultureInfo.InvariantCulture);
            ChkAutoSave.IsChecked = _settingsManager.AiAutoSave;
            ChkAutoRun.IsChecked = _settingsManager.AiAutoRun;
            ChkVerifyOnSave.IsChecked = _settingsManager.AiVerifyOnSave;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error loading AI settings");
        }
        finally
        {
            _loading = false;
        }
    }

    private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_loading) return;

            var provider = SelectedProvider;

            var currentBaseUrl = TxtBaseUrl.Text.Trim();
            if (string.IsNullOrEmpty(currentBaseUrl) || IsKnownDefaultBaseUrl(currentBaseUrl))
                TxtBaseUrl.Text = DefaultBaseUrl(provider);

            var currentModel = TxtModel.Text.Trim();
            if (string.IsNullOrEmpty(currentModel) || IsKnownDefaultModel(currentModel))
                TxtModel.Text = DefaultModel(provider);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error applying AI provider preset");
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var provider = SelectedProvider;
            var baseUrl = TxtBaseUrl.Text.Trim();
            var apiKey = TxtApiKey.Text.Trim();
            var enabled = ChkAiEnabled.IsChecked == true;

            if (!string.IsNullOrEmpty(baseUrl) && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    "Please enter a valid AI Base URL, for example:\n\nhttps://openrouter.ai/api/v1\nhttp://localhost:11434/v1",
                    "Invalid Base URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (enabled && string.Equals(provider, AppConstants.AiProviders.OpenRouter, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show(
                    "An API key is required when using OpenRouter.\n\nEnter your key or switch the provider to Local.",
                    "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settingsManager.AiAssistEnabled = enabled;
            _settingsManager.AiProvider = provider;
            _settingsManager.AiBaseUrl = baseUrl;
            _settingsManager.AiApiKey = apiKey;
            _settingsManager.AiModel = TxtModel.Text.Trim();
            _settingsManager.AiTimeoutSeconds = ParseInt(TxtTimeout.Text, _settingsManager.AiTimeoutSeconds);
            _settingsManager.AiMaxCandidates = ParseInt(TxtMaxCandidates.Text, _settingsManager.AiMaxCandidates);
            _settingsManager.AiImageMaxDimension =
                ParseInt(TxtImageMaxDimension.Text, _settingsManager.AiImageMaxDimension);
            _settingsManager.AiAutoSaveThreshold =
                ParseDouble(TxtAutoSaveThreshold.Text, _settingsManager.AiAutoSaveThreshold);
            _settingsManager.AiAutoSave = ChkAutoSave.IsChecked == true;
            _settingsManager.AiAutoRun = ChkAutoRun.IsChecked == true;
            _settingsManager.AiVerifyOnSave = ChkVerifyOnSave.IsChecked == true;

            _settingsManager.SaveSettings();
            LogService.Information("AI settings saved successfully.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("There was an error saving the AI settings.\n\n" +
                            "The developer will try to fix this.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            LogService.Error(ex, "Error saving AI settings.");
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!UrlService.TryOpenUrl(e.Uri.AbsoluteUri))
            MessageBox.Show("Could not open the link. Please copy and paste the URL into your browser.", "Link Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);

        e.Handled = true;
    }

    private static string DefaultBaseUrl(string provider)
    {
        return string.Equals(provider, AppConstants.AiProviders.Local, StringComparison.Ordinal)
            ? AppConstants.AiProviders.LocalBaseUrl
            : AppConstants.AiProviders.OpenRouterBaseUrl;
    }

    private static string DefaultModel(string provider)
    {
        return string.Equals(provider, AppConstants.AiProviders.Local, StringComparison.Ordinal)
            ? AppConstants.AiProviders.DefaultLocalModel
            : AppConstants.AiProviders.DefaultOpenRouterModel;
    }

    private static bool IsKnownDefaultBaseUrl(string baseUrl)
    {
        return string.Equals(baseUrl, AppConstants.AiProviders.OpenRouterBaseUrl, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(baseUrl, AppConstants.AiProviders.LocalBaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownDefaultModel(string model)
    {
        return string.Equals(model, AppConstants.AiProviders.DefaultOpenRouterModel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(model, AppConstants.AiProviders.DefaultLocalModel, StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static double ParseDouble(string text, double fallback)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
