using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

public partial class AiSettingsWindow
{
    private const string ModelCacheFileName = "ai-models.json";
    private static readonly TimeSpan ModelCacheTtl = TimeSpan.FromDays(7);

    private readonly SettingsManager _settingsManager;
    private List<VisionModelInfo> _allModels = [];
    private readonly bool _initialized;
    private bool _loading;
    private AiVerdictCache? _modelCache;
    private AiQueryHistory? _queryHistory;

    public AiSettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        LoadSettings();
        _initialized = true;
        LoadCachedModels();
    }

    private string SelectedProvider => CmbProvider.SelectedItem as string ?? AppConstants.AiProviders.OpenRouter;

    private string ModelCacheKey => $"{SelectedProvider}|{TxtBaseUrl.Text.Trim().ToLowerInvariant()}";

    private AiVerdictCache ModelCache => _modelCache ??= new AiVerdictCache(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FindRomCover",
            ModelCacheFileName),
        ModelCacheTtl);

    private AiQueryHistory QueryHistory => _queryHistory ??= new AiQueryHistory();

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
            CmbModel.Text = _settingsManager.AiModel;
            TxtTimeout.Text = _settingsManager.AiTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            TxtMaxCandidates.Text = _settingsManager.AiMaxCandidates.ToString(CultureInfo.InvariantCulture);
            TxtImageMaxDimension.Text = _settingsManager.AiImageMaxDimension.ToString(CultureInfo.InvariantCulture);
            TxtCandidateThreshold.Text = _settingsManager.AiCandidateThreshold.ToString(CultureInfo.InvariantCulture);
            TxtAutoSaveThreshold.Text = _settingsManager.AiAutoSaveThreshold.ToString(CultureInfo.InvariantCulture);
            ChkAutoSave.IsChecked = _settingsManager.AiAutoSave;
            ChkAutoRun.IsChecked = _settingsManager.AiAutoRun;
            ChkVerifyOnSave.IsChecked = _settingsManager.AiVerifyOnSave;
            ChkSkipQueried.IsChecked = _settingsManager.AiSkipPreviouslyQueried;
            UpdateHistoryCount();
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

            var currentModel = CmbModel.Text.Trim();
            if (string.IsNullOrEmpty(currentModel) || IsKnownDefaultModel(currentModel))
                CmbModel.Text = DefaultModel(provider);

            if (_initialized) LoadCachedModels();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error applying AI provider preset");
        }
    }

    private void TxtBaseUrl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initialized) LoadCachedModels();
    }

    private void TxtModelFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initialized) ApplyModelFilter();
    }

    private void ChkVisionOnly_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialized) ApplyModelFilter();
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

            var isCustomProvider = provider is AppConstants.AiProviders.CustomOpenAi
                or AppConstants.AiProviders.CustomAnthropic;

            if (enabled && isCustomProvider &&
                (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(CmbModel.Text)))
            {
                MessageBox.Show(
                    "A Base URL and a Model are required for custom providers.",
                    "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (enabled && !isCustomProvider &&
                !string.Equals(provider, AppConstants.AiProviders.Local, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show(
                    "An API key is required for OpenRouter, OpenAI, Anthropic, Gemini and GLM.\n\n" +
                    "Enter your key or switch the provider to Local.",
                    "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settingsManager.AiAssistEnabled = enabled;
            _settingsManager.AiProvider = provider;
            _settingsManager.AiBaseUrl = baseUrl;
            _settingsManager.AiApiKey = apiKey;
            _settingsManager.AiModel = CmbModel.Text.Trim();
            _settingsManager.AiTimeoutSeconds = ParseInt(TxtTimeout.Text, _settingsManager.AiTimeoutSeconds);
            _settingsManager.AiMaxCandidates = ParseInt(TxtMaxCandidates.Text, _settingsManager.AiMaxCandidates);
            _settingsManager.AiImageMaxDimension =
                ParseInt(TxtImageMaxDimension.Text, _settingsManager.AiImageMaxDimension);
            _settingsManager.AiCandidateThreshold =
                ParseDouble(TxtCandidateThreshold.Text, _settingsManager.AiCandidateThreshold);
            _settingsManager.AiAutoSaveThreshold =
                ParseDouble(TxtAutoSaveThreshold.Text, _settingsManager.AiAutoSaveThreshold);
            _settingsManager.AiAutoSave = ChkAutoSave.IsChecked == true;
            _settingsManager.AiAutoRun = ChkAutoRun.IsChecked == true;
            _settingsManager.AiVerifyOnSave = ChkVerifyOnSave.IsChecked == true;
            _settingsManager.AiSkipPreviouslyQueried = ChkSkipQueried.IsChecked == true;

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

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var provider = SelectedProvider;
            var baseUrl = TxtBaseUrl.Text.Trim();

            if (!string.IsNullOrEmpty(baseUrl) && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    "Please enter a valid AI Base URL, for example:\n\nhttps://openrouter.ai/api/v1\nhttp://localhost:11434/v1",
                    "Invalid Base URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var options = new AiVisionOptions(
                true,
                provider,
                string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl(provider) : baseUrl,
                TxtApiKey.Text.Trim(),
                string.IsNullOrWhiteSpace(CmbModel.Text) ? DefaultModel(provider) : CmbModel.Text.Trim(),
                ParseInt(TxtTimeout.Text, 90),
                6,
                512,
                80,
                false,
                false,
                false,
                70);

            BtnTest.IsEnabled = false;
            TxtTestStatus.Text = "Contacting provider...";

            using var httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            var models = await VisionModelCatalog.FetchModelsAsync(options, httpClient, CancellationToken.None);

            _allModels = models;
            ModelCache.Set(ModelCacheKey, models);
            ApplyModelFilter();

            var modelAvailable = models.Any(model =>
                string.Equals(model.Id, options.Model, StringComparison.OrdinalIgnoreCase));
            var visionCount = models.Count(static model => model.IsVisionCapable);

            TxtTestStatus.Text = models.Count == 0
                ? "Connected, but the provider returned no models."
                : modelAvailable
                    ? $"Connected — {models.Count} model(s) ({visionCount} vision-capable). '{options.Model}' is available."
                    : $"Connected — {models.Count} model(s) ({visionCount} vision-capable), but '{options.Model}' was not in the list.";

            LogService.Information(
                $"AI connection test succeeded for {provider}: {models.Count} model(s), {visionCount} vision-capable.");
        }
        catch (Exception ex)
        {
            TxtTestStatus.Text = ex.Message;
            LogService.Warning(ex, "AI connection test failed.");
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var count = QueryHistory.Count;
            if (count == 0)
            {
                UpdateHistoryCount();
                return;
            }

            var choice = MessageBox.Show(
                $"Forget the {count} missing cover(s) that were already queried?\n\n" +
                "The AI may query them again in the next batch run.",
                "Clear AI Query History", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (choice != MessageBoxResult.Yes) return;

            QueryHistory.Clear();
            UpdateHistoryCount();
            LogService.Information($"AI query history cleared ({count} entries).");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error clearing the AI query history.");
        }
    }

    private void UpdateHistoryCount()
    {
        var count = QueryHistory.Count;
        TxtHistoryCount.Text = count == 0
            ? "No queried covers remembered."
            : $"{count} queried cover(s) remembered.";
    }

    private void LoadCachedModels()
    {
        try
        {
            if (ModelCache.TryGet<List<VisionModelInfo>>(ModelCacheKey, out var cached) && cached is { Count: > 0 })
            {
                _allModels = cached;
                ApplyModelFilter();
                TxtModelCount.Text =
                    $"{_allModels.Count} cached model(s) loaded. Click Test / Load Models to refresh.";
                return;
            }
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Could not load the cached AI model list.");
        }

        _allModels = [];
        ApplyModelFilter();
        TxtModelCount.Text = string.Empty;
    }

    private void ApplyModelFilter()
    {
        try
        {
            var filter = TxtModelFilter.Text.Trim();
            var visionOnly = ChkVisionOnly.IsChecked == true;

            var filtered = _allModels
                .Where(model => !visionOnly || model.IsVisionCapable)
                .Where(model => string.IsNullOrEmpty(filter) ||
                                model.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(static model => model.Id)
                .ToList();

            var currentModel = CmbModel.Text;
            CmbModel.ItemsSource = filtered;
            CmbModel.Text = currentModel;

            if (_allModels.Count > 0)
                TxtModelCount.Text = $"{filtered.Count} of {_allModels.Count} model(s) shown.";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error filtering the AI model list.");
        }
    }

    private static string DefaultBaseUrl(string provider)
    {
        return provider switch
        {
            AppConstants.AiProviders.OpenAi => AppConstants.AiProviders.OpenAiBaseUrl,
            AppConstants.AiProviders.Anthropic => AppConstants.AiProviders.AnthropicBaseUrl,
            AppConstants.AiProviders.CustomAnthropic => AppConstants.AiProviders.AnthropicBaseUrl,
            AppConstants.AiProviders.Gemini => AppConstants.AiProviders.GeminiBaseUrl,
            AppConstants.AiProviders.Glm => AppConstants.AiProviders.GlmBaseUrl,
            AppConstants.AiProviders.Local => AppConstants.AiProviders.LocalBaseUrl,
            AppConstants.AiProviders.CustomOpenAi => string.Empty,
            _ => AppConstants.AiProviders.OpenRouterBaseUrl
        };
    }

    private static string DefaultModel(string provider)
    {
        return provider switch
        {
            AppConstants.AiProviders.OpenAi => AppConstants.AiProviders.DefaultOpenAiModel,
            AppConstants.AiProviders.Anthropic => AppConstants.AiProviders.DefaultAnthropicModel,
            AppConstants.AiProviders.Gemini => AppConstants.AiProviders.DefaultGeminiModel,
            AppConstants.AiProviders.Glm => AppConstants.AiProviders.DefaultGlmModel,
            AppConstants.AiProviders.Local => AppConstants.AiProviders.DefaultLocalModel,
            AppConstants.AiProviders.CustomAnthropic => string.Empty,
            AppConstants.AiProviders.CustomOpenAi => string.Empty,
            _ => AppConstants.AiProviders.DefaultOpenRouterModel
        };
    }

    private static bool IsKnownDefaultBaseUrl(string baseUrl)
    {
        return new[]
        {
            AppConstants.AiProviders.OpenRouterBaseUrl,
            AppConstants.AiProviders.OpenAiBaseUrl,
            AppConstants.AiProviders.AnthropicBaseUrl,
            AppConstants.AiProviders.GeminiBaseUrl,
            AppConstants.AiProviders.GlmBaseUrl,
            AppConstants.AiProviders.LocalBaseUrl
        }.Contains(baseUrl, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsKnownDefaultModel(string model)
    {
        return new[]
        {
            AppConstants.AiProviders.DefaultOpenRouterModel,
            AppConstants.AiProviders.DefaultOpenAiModel,
            AppConstants.AiProviders.DefaultAnthropicModel,
            AppConstants.AiProviders.DefaultGeminiModel,
            AppConstants.AiProviders.DefaultGlmModel,
            AppConstants.AiProviders.DefaultLocalModel
        }.Contains(model, StringComparer.OrdinalIgnoreCase);
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
