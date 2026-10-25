using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover.Managers;

/// <summary>
///     Loads, validates and persists the application settings in a SQLite database and raises
///     change notifications for bound UI elements.
/// </summary>
public class SettingsManager : INotifyPropertyChanged
{
    /// <summary>
    ///     Set by the GUI head to display settings-save failures (message, title).
    ///     When null, failures are only logged through <see cref="ErrorLogger" />.
    /// </summary>
    public static Action<string, string>? ErrorReporter { get; set; }

    private static SettingsManager? _currentInstance;
    private static readonly Lock InstanceLock = new();

    private static readonly string DefaultSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FindRomCover");

    /// <summary>
    ///     Overrides the default settings directory. Used by tests so they never touch the
    ///     real user profile.
    /// </summary>
    internal static string? DefaultSettingsDirectoryOverride { get; set; }

    private static readonly string LegacyAppDirSettingsFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.SettingsFileName);

    private static readonly byte[] EncryptionKey = DeriveEncryptionKey();

    private readonly string? _settingsDirectory;

    private string SettingsDatabasePath =>
        Path.Combine(_settingsDirectory ?? DefaultSettingsDirectoryOverride ?? DefaultSettingsDirectory,
            AppConstants.SettingsDatabaseFileName);

    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    // --- Theme settings ---

    private static readonly HashSet<string> ValidBaseThemes = new(StringComparer.OrdinalIgnoreCase) { "Light", "Dark" };

    private static readonly HashSet<string> ValidAccentColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Red", "Green", "Blue", "Orange", "Purple", "Pink", "Lime", "Emerald",
        "Teal", "Cyan", "Cobalt", "Indigo", "Violet", "Magenta", "Crimson",
        "Amber", "Yellow", "Brown", "Olive", "Steel", "Mauve", "Taupe", "Sienna"
    };

    private readonly Lock _ioLock = new();
    private bool _databaseUnavailable;

    private bool _aiAssistEnabled;

    private string _aiApiKey = string.Empty;

    private double _aiAutoSaveThreshold = 80;

    private bool _aiAutoRun;

    private bool _aiAutoSave;

    private string _aiBaseUrl = string.Empty;

    private double _aiCandidateThreshold = 70;

    private int _aiImageMaxDimension = 512;

    private int _aiMinCoverWidth = 200;

    private int _aiMaxCandidates = 6;

    private string _aiModel = string.Empty;

    private string _aiProvider = AppConstants.AiProviders.OpenRouter;

    private string _aiRequestParameters = string.Empty;

    private bool _aiSkipPreviouslyQueried = true;

    private int _aiTimeoutSeconds = 90;

    private bool _aiVerifyOnSave;

    private string _accentColor = "Blue";

    private int _apiTimeoutSeconds = 30;

    private string _baseTheme = "Dark";

    private string _bugReportApiKey = AppConstants.BugReportApiKey;

    private string _bugReportApiUrl = AppConstants.BugReportApiUrl;

    private string _googleKey = string.Empty;

    private bool _ignoreBracketedText = true;

    private int _imageHeight = 300;

    private int _imageLoaderMaxRetries = 3;

    private int _imageLoaderRetryDelayMilliseconds = 200;

    // --- Thumbnail settings (merged: ImageWidth/ImageHeight from FindRomCover, ThumbnailSize from FindRomCover) ---

    private int _imageWidth = 300;

    private string _lastImageFolder = string.Empty;

    private int _maxImagesToLoad = 30;

    // --- FindRomCover-specific settings ---

    private string _searchEngine = "BingWeb";

    private string _selectedSimilarityAlgorithm = string.Empty;

    // --- Similarity settings (from FindRomCover) ---

    private double _similarityThreshold;

    // --- Extensions ---

    private List<string> _supportedExtensions = [];

    // --- MAME settings ---

    private bool _useMameDescriptions;

    // --- Constructor and Load/Save ---

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsManager" /> class using the default settings location.
    /// </summary>
    public SettingsManager()
        : this(null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsManager" /> class with an optional custom settings directory.
    /// </summary>
    /// <param name="settingsDirectory">The directory that stores the settings database, or <c>null</c> to use the default location.</param>
    public SettingsManager(string? settingsDirectory)
    {
        _settingsDirectory = settingsDirectory;

        lock (InstanceLock)
        {
            _currentInstance = this;
        }

        LoadSettings();
    }

    /// <summary>
    ///     Gets the most recently created <see cref="SettingsManager" /> instance, or <c>null</c> when none exists.
    /// </summary>
    public static SettingsManager? CurrentInstance
    {
        get
        {
            lock (InstanceLock)
            {
                return _currentInstance;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the similarity threshold (0-100) used when comparing image names.
    /// </summary>
    public double SimilarityThreshold
    {
        get => _similarityThreshold;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (Math.Abs(_similarityThreshold - value) < 0.01) return;

            _similarityThreshold = value;
            OnPropertyChanged(nameof(SimilarityThreshold));
        }
    }

    /// <summary>
    ///     Gets or sets the name of the similarity algorithm used to compare file names.
    /// </summary>
    public string SelectedSimilarityAlgorithm
    {
        get => _selectedSimilarityAlgorithm;
        set
        {
            if (string.Equals(_selectedSimilarityAlgorithm, value, StringComparison.OrdinalIgnoreCase)) return;

            _selectedSimilarityAlgorithm = value;
            OnPropertyChanged(nameof(SelectedSimilarityAlgorithm));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether text inside brackets is ignored during similarity comparison.
    /// </summary>
    public bool IgnoreBracketedText
    {
        get => _ignoreBracketedText;
        set
        {
            if (_ignoreBracketedText == value) return;

            _ignoreBracketedText = value;
            OnPropertyChanged(nameof(IgnoreBracketedText));
        }
    }

    /// <summary>
    ///     Gets or sets the maximum number of images loaded per search (1-1000).
    /// </summary>
    public int MaxImagesToLoad
    {
        get => _maxImagesToLoad;
        set
        {
            value = Math.Clamp(value, 1, 1000);
            if (_maxImagesToLoad == value) return;

            _maxImagesToLoad = value;
            OnPropertyChanged(nameof(MaxImagesToLoad));
        }
    }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts for image downloads (0-20).
    /// </summary>
    public int ImageLoaderMaxRetries
    {
        get => _imageLoaderMaxRetries;
        set
        {
            value = Math.Clamp(value, 0, 20);
            if (_imageLoaderMaxRetries == value) return;

            _imageLoaderMaxRetries = value;
            OnPropertyChanged(nameof(ImageLoaderMaxRetries));
        }
    }

    /// <summary>
    ///     Gets or sets the delay in milliseconds between image download retry attempts (0-10000).
    /// </summary>
    public int ImageLoaderRetryDelayMilliseconds
    {
        get => _imageLoaderRetryDelayMilliseconds;
        set
        {
            value = Math.Clamp(value, 0, 10000);
            if (_imageLoaderRetryDelayMilliseconds == value) return;

            _imageLoaderRetryDelayMilliseconds = value;
            OnPropertyChanged(nameof(ImageLoaderRetryDelayMilliseconds));
        }
    }

    /// <summary>
    ///     Gets or sets the timeout in seconds for image search API requests (1-300).
    /// </summary>
    public int ApiTimeoutSeconds
    {
        get => _apiTimeoutSeconds;
        set
        {
            value = Math.Clamp(value, 1, 300);
            if (_apiTimeoutSeconds == value) return;

            _apiTimeoutSeconds = value;
            OnPropertyChanged(nameof(ApiTimeoutSeconds));
        }
    }

    /// <summary>
    ///     Gets or sets the path of the folder last used for image operations.
    /// </summary>
    public string LastImageFolder
    {
        get => _lastImageFolder;
        set
        {
            if (string.Equals(_lastImageFolder, value, StringComparison.OrdinalIgnoreCase)) return;

            _lastImageFolder = value;
            OnPropertyChanged(nameof(LastImageFolder));
        }
    }

    /// <summary>
    ///     Gets or sets the target image width in pixels (50-2000).
    /// </summary>
    public int ImageWidth
    {
        get => _imageWidth;
        set
        {
            value = Math.Clamp(value, 50, 2000);
            if (_imageWidth == value) return;

            _imageWidth = value;
            OnPropertyChanged(nameof(ImageWidth));
        }
    }

    /// <summary>
    ///     Gets or sets the target image height in pixels (50-2000).
    /// </summary>
    public int ImageHeight
    {
        get => _imageHeight;
        set
        {
            value = Math.Clamp(value, 50, 2000);
            if (_imageHeight == value) return;

            _imageHeight = value;
            OnPropertyChanged(nameof(ImageHeight));
        }
    }

    /// <summary>
    ///     Gets or sets the square thumbnail size; setting it updates both <see cref="ImageWidth" /> and <see cref="ImageHeight" />.
    /// </summary>
    public int ThumbnailSize
    {
        get => Math.Max(_imageWidth, _imageHeight);
        set
        {
            // Guard the round trip: assigning the current value must not silently
            // normalize a non-square configuration.
            if (value == Math.Max(_imageWidth, _imageHeight)) return;

            ImageWidth = value;
            ImageHeight = value;
        }
    }

    /// <summary>
    ///     Gets or sets the base theme name ("Light" or "Dark"); invalid values fall back to "Dark".
    /// </summary>
    public string BaseTheme
    {
        get => _baseTheme;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidBaseThemes.Contains(value)) value = "Dark";

            if (string.Equals(_baseTheme, value, StringComparison.OrdinalIgnoreCase)) return;

            _baseTheme = value;
            OnPropertyChanged(nameof(BaseTheme));
        }
    }

    /// <summary>
    ///     Gets or sets the accent color name; invalid values fall back to "Blue".
    /// </summary>
    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidAccentColors.Contains(value)) value = "Blue";

            if (string.Equals(_accentColor, value, StringComparison.OrdinalIgnoreCase)) return;

            _accentColor = value;
            OnPropertyChanged(nameof(AccentColor));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether MAME descriptions are used when naming images.
    /// </summary>
    public bool UseMameDescriptions
    {
        get => _useMameDescriptions;
        set
        {
            if (_useMameDescriptions == value) return;

            _useMameDescriptions = value;
            OnPropertyChanged(nameof(UseMameDescriptions));
        }
    }

    /// <summary>
    ///     Gets or sets the list of file extensions treated as supported ROM files.
    /// </summary>
    public List<string> SupportedExtensions
    {
        get => _supportedExtensions;
        set
        {
            var normalized = value
                .Select(static extension => extension.Trim().TrimStart('.'))
                .Where(static extension => !string.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_supportedExtensions.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase)) return;

            _supportedExtensions = normalized;
            OnPropertyChanged(nameof(SupportedExtensions));
        }
    }

    /// <summary>
    ///     Gets or sets the name of the image search engine provider.
    /// </summary>
    public string SearchEngine
    {
        get => _searchEngine;
        set
        {
            if (string.Equals(_searchEngine, value, StringComparison.OrdinalIgnoreCase)) return;

            _searchEngine = value;
            OnPropertyChanged(nameof(SearchEngine));
        }
    }

    /// <summary>
    ///     Gets or sets the API key used to submit bug reports.
    /// </summary>
    public string BugReportApiKey
    {
        get => _bugReportApiKey;
        set
        {
            if (string.Equals(_bugReportApiKey, value, StringComparison.OrdinalIgnoreCase)) return;

            _bugReportApiKey = value;
            OnPropertyChanged(nameof(BugReportApiKey));
        }
    }

    /// <summary>
    ///     Gets or sets the endpoint URL used to submit bug reports.
    /// </summary>
    public string BugReportApiUrl
    {
        get => _bugReportApiUrl;
        set
        {
            if (string.Equals(_bugReportApiUrl, value, StringComparison.OrdinalIgnoreCase)) return;

            _bugReportApiUrl = value;
            OnPropertyChanged(nameof(BugReportApiUrl));
        }
    }

    /// <summary>
    ///     Gets or sets the Google Custom Search API key.
    /// </summary>
    public string GoogleKey
    {
        get => _googleKey;
        set
        {
            if (string.Equals(_googleKey, value, StringComparison.OrdinalIgnoreCase)) return;

            _googleKey = value;
            OnPropertyChanged(nameof(GoogleKey));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether AI-assisted cover selection is enabled.
    /// </summary>
    public bool AiAssistEnabled
    {
        get => _aiAssistEnabled;
        set
        {
            if (_aiAssistEnabled == value) return;

            _aiAssistEnabled = value;
            OnPropertyChanged(nameof(AiAssistEnabled));
        }
    }

    /// <summary>
    ///     Gets or sets the configured AI provider; unknown values fall back to the default provider.
    /// </summary>
    public string AiProvider
    {
        get => _aiProvider;
        set
        {
            var normalized = AppConstants.AiProviders.All.Contains(value, StringComparer.OrdinalIgnoreCase)
                ? AppConstants.AiProviders.All.First(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase))
                : AppConstants.AiProviders.OpenRouter;

            if (string.Equals(_aiProvider, normalized, StringComparison.Ordinal)) return;

            _aiProvider = normalized;
            OnPropertyChanged(nameof(AiProvider));
        }
    }

    /// <summary>
    ///     Gets or sets the custom AI API base URL; empty means the provider default is used.
    /// </summary>
    [AllowNull]
    public string AiBaseUrl
    {
        get => _aiBaseUrl;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if (string.Equals(_aiBaseUrl, value, StringComparison.Ordinal)) return;

            _aiBaseUrl = value;
            OnPropertyChanged(nameof(AiBaseUrl));
        }
    }

    /// <summary>
    ///     Gets or sets the API key used for AI vision requests.
    /// </summary>
    [AllowNull]
    public string AiApiKey
    {
        get => _aiApiKey;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if (string.Equals(_aiApiKey, value, StringComparison.Ordinal)) return;

            _aiApiKey = value;
            OnPropertyChanged(nameof(AiApiKey));
        }
    }

    /// <summary>
    ///     Gets or sets the AI model name; empty means the provider default is used.
    /// </summary>
    [AllowNull]
    public string AiModel
    {
        get => _aiModel;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if (string.Equals(_aiModel, value, StringComparison.Ordinal)) return;

            _aiModel = value;
            OnPropertyChanged(nameof(AiModel));
        }
    }

    /// <summary>
    ///     Gets or sets optional JSON request parameters merged into every AI provider
    ///     request body. Empty means the provider defaults are used.
    /// </summary>
    [AllowNull]
    public string AiRequestParameters
    {
        get => _aiRequestParameters;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if (string.Equals(_aiRequestParameters, value, StringComparison.Ordinal)) return;

            _aiRequestParameters = value;
            OnPropertyChanged(nameof(AiRequestParameters));
        }
    }

    /// <summary>
    ///     Gets or sets the AI request timeout in seconds (10-300).
    /// </summary>
    public int AiTimeoutSeconds
    {
        get => _aiTimeoutSeconds;
        set
        {
            value = Math.Clamp(value, 10, 300);
            if (_aiTimeoutSeconds == value) return;

            _aiTimeoutSeconds = value;
            OnPropertyChanged(nameof(AiTimeoutSeconds));
        }
    }

    /// <summary>
    ///     Gets or sets the maximum number of cover candidates requested from the AI (1-20).
    /// </summary>
    public int AiMaxCandidates
    {
        get => _aiMaxCandidates;
        set
        {
            value = Math.Clamp(value, 1, 20);
            if (_aiMaxCandidates == value) return;

            _aiMaxCandidates = value;
            OnPropertyChanged(nameof(AiMaxCandidates));
        }
    }

    /// <summary>
    ///     Gets or sets the maximum dimension in pixels of images sent to the AI (128-2048).
    /// </summary>
    public int AiImageMaxDimension
    {
        get => _aiImageMaxDimension;
        set
        {
            value = Math.Clamp(value, 128, 2048);
            if (_aiImageMaxDimension == value) return;

            _aiImageMaxDimension = value;
            OnPropertyChanged(nameof(AiImageMaxDimension));
        }
    }

    /// <summary>
    ///     Gets or sets the minimum accepted cover width in pixels (0-2048).
    /// </summary>
    public int AiMinCoverWidth
    {
        get => _aiMinCoverWidth;
        set
        {
            value = Math.Clamp(value, 0, 2048);
            if (_aiMinCoverWidth == value) return;

            _aiMinCoverWidth = value;
            OnPropertyChanged(nameof(AiMinCoverWidth));
        }
    }

    /// <summary>
    ///     Gets or sets the similarity score (0-100) an AI candidate must reach to be accepted.
    /// </summary>
    public double AiCandidateThreshold
    {
        get => _aiCandidateThreshold;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (Math.Abs(_aiCandidateThreshold - value) < 0.01) return;

            _aiCandidateThreshold = value;
            OnPropertyChanged(nameof(AiCandidateThreshold));
        }
    }

    /// <summary>
    ///     Gets or sets the similarity score (0-100) at which an AI candidate is saved automatically.
    /// </summary>
    public double AiAutoSaveThreshold
    {
        get => _aiAutoSaveThreshold;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (Math.Abs(_aiAutoSaveThreshold - value) < 0.01) return;

            _aiAutoSaveThreshold = value;
            OnPropertyChanged(nameof(AiAutoSaveThreshold));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether accepted AI candidates are saved automatically.
    /// </summary>
    public bool AiAutoSave
    {
        get => _aiAutoSave;
        set
        {
            if (_aiAutoSave == value) return;

            _aiAutoSave = value;
            OnPropertyChanged(nameof(AiAutoSave));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the AI cover search runs automatically.
    /// </summary>
    public bool AiAutoRun
    {
        get => _aiAutoRun;
        set
        {
            if (_aiAutoRun == value) return;

            _aiAutoRun = value;
            OnPropertyChanged(nameof(AiAutoRun));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether AI-selected covers are verified before saving.
    /// </summary>
    public bool AiVerifyOnSave
    {
        get => _aiVerifyOnSave;
        set
        {
            if (_aiVerifyOnSave == value) return;

            _aiVerifyOnSave = value;
            OnPropertyChanged(nameof(AiVerifyOnSave));
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether image names already queried in earlier AI searches are skipped.
    /// </summary>
    public bool AiSkipPreviouslyQueried
    {
        get => _aiSkipPreviouslyQueried;
        set
        {
            if (_aiSkipPreviouslyQueried == value) return;

            _aiSkipPreviouslyQueried = value;
            OnPropertyChanged(nameof(AiSkipPreviouslyQueried));
        }
    }

    /// <summary>
    ///     Returns the AI base URL to use, falling back to the default URL of the selected provider.
    /// </summary>
    /// <returns>The configured base URL, or the provider-specific default URL.</returns>
    public string GetEffectiveAiBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_aiBaseUrl)) return _aiBaseUrl.Trim();

        return _aiProvider switch
        {
            AppConstants.AiProviders.OpenAi => AppConstants.AiProviders.OpenAiBaseUrl,
            AppConstants.AiProviders.Anthropic => AppConstants.AiProviders.AnthropicBaseUrl,
            AppConstants.AiProviders.Gemini => AppConstants.AiProviders.GeminiBaseUrl,
            AppConstants.AiProviders.Glm => AppConstants.AiProviders.GlmBaseUrl,
            AppConstants.AiProviders.Local => AppConstants.AiProviders.LocalBaseUrl,
            AppConstants.AiProviders.CustomAnthropic => AppConstants.AiProviders.AnthropicBaseUrl,
            AppConstants.AiProviders.CustomOpenAi => string.Empty,
            _ => AppConstants.AiProviders.OpenRouterBaseUrl
        };
    }

    /// <summary>
    ///     Returns the AI model name to use, falling back to the default model of the selected provider.
    /// </summary>
    /// <returns>The configured model name, or the provider-specific default model.</returns>
    public string GetEffectiveAiModel()
    {
        var configuredModel = NormalizeModelId(_aiModel);
        if (!string.IsNullOrWhiteSpace(configuredModel)) return configuredModel;

        return _aiProvider switch
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

    /// <summary>
    ///     Collapses pasted multi-model text (for example the whole recommended-models list
    ///     from the documentation) to its first model ID. Providers reject the raw pasted
    ///     list with a confusing "not a valid model ID" error (issue #68255).
    /// </summary>
    /// <param name="model">The raw configured model text.</param>
    /// <returns>The first whitespace-separated token, or an empty string when none exists.</returns>
    internal static string NormalizeModelId(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return string.Empty;

        var tokens = model.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length == 0 ? string.Empty : tokens[0];
    }

    /// <summary>
    ///     Creates a snapshot of the AI-related settings for use by the vision client.
    /// </summary>
    /// <returns>An <see cref="AiVisionOptions" /> instance populated from the current settings.</returns>
    public AiVisionOptions GetAiVisionOptions()
    {
        return new AiVisionOptions(
            AiAssistEnabled,
            AiProvider,
            GetEffectiveAiBaseUrl(),
            AiApiKey,
            GetEffectiveAiModel(),
            AiTimeoutSeconds,
            AiMaxCandidates,
            AiImageMaxDimension,
            AiAutoSaveThreshold,
            AiAutoSave,
            AiAutoRun,
            AiVerifyOnSave,
            AiCandidateThreshold,
            AiRequestParameters);
    }

    /// <summary>
    ///     Occurs when a settings property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static byte[] DeriveEncryptionKey()
    {
        var machineName = Environment.MachineName;
        const string appName = "FindRomCover";
        var salt = Encoding.UTF8.GetBytes($"{machineName}_{appName}_settings");
        const string password = "G4m3C0v3rScr4p3r_S3tt1ngs_K3y_2024";

        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                10000,
                HashAlgorithmName.SHA256,
                32);
        }
        catch (Exception ex)
        {
            // The OS BCrypt/CNG PBKDF2 implementation can fail on some systems
            // (e.g. CryptographicException 0xc1000008), which previously crashed
            // the type initializer and prevented the app from starting at all.
            // Fall back to a fully managed PBKDF2-HMAC-SHA256 implementation,
            // which produces the exact same key (RFC 2898).
            try
            {
                LogService.WarningTransient(ex, "OS PBKDF2 failed; falling back to managed PBKDF2-HMAC-SHA256.");
            }
            catch
            {
                // Logging must never break key derivation.
            }

            return ManagedPbkdf2HmacSha256(Encoding.UTF8.GetBytes(password), salt, 10000, 32);
        }
    }

    private static byte[] ManagedPbkdf2HmacSha256(byte[] password, byte[] salt, int iterations, int outputLength)
    {
        using var hmac = new HMACSHA256(password);
        var hashLength = hmac.HashSize / 8;
        if (hashLength <= 0) throw new CryptographicException("Invalid HMAC hash size.");

        var blockCount = (outputLength + hashLength - 1) / hashLength;
        var derivedKey = new byte[blockCount * hashLength];

        var saltAndBlockIndex = new byte[salt.Length + 4];
        Buffer.BlockCopy(salt, 0, saltAndBlockIndex, 0, salt.Length);

        for (var block = 1; block <= blockCount; block++)
        {
            saltAndBlockIndex[salt.Length] = (byte)(block >> 24);
            saltAndBlockIndex[salt.Length + 1] = (byte)(block >> 16);
            saltAndBlockIndex[salt.Length + 2] = (byte)(block >> 8);
            saltAndBlockIndex[salt.Length + 3] = (byte)block;

            var u = hmac.ComputeHash(saltAndBlockIndex);
            var t = new byte[hashLength];
            Buffer.BlockCopy(u, 0, t, 0, hashLength);

            for (var iteration = 1; iteration < iterations; iteration++)
            {
                u = hmac.ComputeHash(u);
                for (var i = 0; i < hashLength; i++) t[i] ^= u[i];
            }

            Buffer.BlockCopy(t, 0, derivedKey, (block - 1) * hashLength, hashLength);
        }

        if (outputLength == derivedKey.Length) return derivedKey;

        var result = new byte[outputLength];
        Buffer.BlockCopy(derivedKey, 0, result, 0, outputLength);
        return result;
    }

    /// <summary>
    ///     Loads the settings from the SQLite database, migrating legacy settings files when necessary
    ///     and falling back to defaults when no readable settings exist.
    /// </summary>
    public void LoadSettings()
    {
        lock (_ioLock)
        {
            try
            {
                if (TryLoadFromDatabase()) return;

                if (TryMigrateLegacySettings()) return;

                // Try to migrate from legacy settings.xml
                if (TryMigrateFromLegacyXml()) return;

                SetDefaultSettings();
                SaveSettingsInternal();
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable settings are an environment issue, not a code bug.
                // Never overwrite an existing database on an unknown failure: keep it for
                // the next start and use defaults for this session only.
                LogService.WarningTransient(ex, "Error loading settings; using defaults for this session.");
                SetDefaultSettings();

                if (File.Exists(SettingsDatabasePath))
                    _databaseUnavailable = true;
                else
                    SaveSettingsInternal();
            }
        }
    }

    private bool TryLoadFromDatabase()
    {
        // A non-SQLite file at the database path is a legacy encrypted settings file; migrate it below.
        if (IsLegacySettingsFile(SettingsDatabasePath)) return false;
        if (!File.Exists(SettingsDatabasePath)) return false;

        var database = new SettingsDatabase(SettingsDatabasePath);
        if (!database.TryLoadAll(out var values, out var isCorrupt))
        {
            if (isCorrupt)
            {
                // The database exists but is genuinely corrupt. Keep it for diagnosis
                // instead of letting LoadSettings overwrite it with defaults.
                QuarantineSettingsFile();
                return false;
            }

            // A transient failure (file lock, antivirus, sharing violation) must not
            // destroy the user's settings. Use defaults for this session and refuse to
            // persist them over the still-valid database.
            LogService.WarningTransient(
                "Settings database is temporarily unavailable; using defaults for this session without overwriting it.");
            _databaseUnavailable = true;
            SetDefaultSettings();
            return true;
        }

        if (values.Count == 0) return false;

        SetDefaultSettings();
        ApplyStoredValues(values);
        return true;
    }

    private bool TryMigrateLegacySettings()
    {
        var legacyPaths = GetLegacySettingsFilePaths();
        if (legacyPaths.Count == 0) return false;

        foreach (var legacyPath in legacyPaths)
        {
            var data = LoadAndDecryptSettings(legacyPath);
            if (data == null)
            {
                // Keep the unreadable file out of the way so a fresh database can be
                // created, then try the next (older) legacy file.
                BackupLegacyFile(legacyPath);
                continue;
            }

            SetDefaultSettings();
            ApplyData(data);

            // The legacy file can occupy the database path itself, so move it aside
            // before creating the database. It is kept as ".legacy" for recovery.
            var backupPath = BackupLegacyFile(legacyPath);

            if (!SaveSettingsInternal())
            {
                // Put the legacy file back so the migration is retried on the next
                // start instead of stranding the settings in the backup.
                if (backupPath != null)
                    try
                    {
                        File.Move(backupPath, legacyPath, true);
                    }
                    catch (Exception restoreEx)
                    {
                        LogService.WarningTransient(restoreEx,
                            $"Could not restore legacy settings file '{backupPath}' after a failed migration.");
                    }

                LogService.WarningTransient(
                    $"Could not migrate legacy settings from '{legacyPath}' into the SQLite settings database; the migration will be retried on the next start.");
                return true;
            }

            LogService.Information($"Migrated legacy settings from '{legacyPath}' into the SQLite settings database.");
            return true;
        }

        return false;
    }

    private void ApplyData(SettingsData data)
    {
        SimilarityThreshold = data.SimilarityThreshold;
        SelectedSimilarityAlgorithm = data.SimilarityAlgorithm;
        IgnoreBracketedText = data.IgnoreBracketedText;
        BaseTheme = data.BaseTheme;
        AccentColor = data.AccentColor;
        ImageWidth = data.ImageWidth;
        ImageHeight = data.ImageHeight;
        MaxImagesToLoad = data.MaxImagesToLoad;
        ImageLoaderMaxRetries = data.ImageLoaderMaxRetries;
        ImageLoaderRetryDelayMilliseconds = data.ImageLoaderRetryDelayMilliseconds;
        ApiTimeoutSeconds = data.ApiTimeoutSeconds;
        SearchEngine = data.SearchEngine;
        BugReportApiKey = string.IsNullOrWhiteSpace(data.BugReportApiKey)
            ? AppConstants.BugReportApiKey
            : data.BugReportApiKey;
        BugReportApiUrl = string.IsNullOrWhiteSpace(data.BugReportApiUrl)
            ? AppConstants.BugReportApiUrl
            : data.BugReportApiUrl;
        GoogleKey = data.GoogleKey;
        UseMameDescriptions = data.UseMameDescriptions;
        LastImageFolder = data.LastImageFolder;
        SupportedExtensions = data.SupportedExtensions is { Count: > 0 }
            ? data.SupportedExtensions
            : GetDefaultExtensions();
        AiAssistEnabled = data.AiAssistEnabled;
        AiProvider = data.AiProvider;
        AiBaseUrl = data.AiBaseUrl;
        AiApiKey = data.AiApiKey;
        AiModel = data.AiModel;
        AiRequestParameters = data.AiRequestParameters;
        AiTimeoutSeconds = data.AiTimeoutSeconds;
        AiMaxCandidates = data.AiMaxCandidates;
        AiImageMaxDimension = data.AiImageMaxDimension;
        AiMinCoverWidth = data.AiMinCoverWidth;
        AiCandidateThreshold = data.AiCandidateThreshold;
        AiAutoSaveThreshold = data.AiAutoSaveThreshold;
        AiAutoSave = data.AiAutoSave;
        AiAutoRun = data.AiAutoRun;
        AiVerifyOnSave = data.AiVerifyOnSave;
        AiSkipPreviouslyQueried = data.AiSkipPreviouslyQueried;
    }

    private void ApplyStoredValues(IReadOnlyDictionary<string, string> values)
    {
        SimilarityThreshold = GetDouble(values, nameof(SimilarityThreshold), SimilarityThreshold);
        SelectedSimilarityAlgorithm =
            GetString(values, nameof(SelectedSimilarityAlgorithm), SelectedSimilarityAlgorithm);
        IgnoreBracketedText = GetBool(values, nameof(IgnoreBracketedText), IgnoreBracketedText);
        BaseTheme = GetString(values, nameof(BaseTheme), BaseTheme);
        AccentColor = GetString(values, nameof(AccentColor), AccentColor);
        ImageWidth = GetInt(values, nameof(ImageWidth), ImageWidth);
        ImageHeight = GetInt(values, nameof(ImageHeight), ImageHeight);
        MaxImagesToLoad = GetInt(values, nameof(MaxImagesToLoad), MaxImagesToLoad);
        ImageLoaderMaxRetries = GetInt(values, nameof(ImageLoaderMaxRetries), ImageLoaderMaxRetries);
        ImageLoaderRetryDelayMilliseconds =
            GetInt(values, nameof(ImageLoaderRetryDelayMilliseconds), ImageLoaderRetryDelayMilliseconds);
        ApiTimeoutSeconds = GetInt(values, nameof(ApiTimeoutSeconds), ApiTimeoutSeconds);
        SearchEngine = GetString(values, nameof(SearchEngine), SearchEngine);
        BugReportApiKey = GetSecret(values, nameof(BugReportApiKey), BugReportApiKey);
        var storedBugReportUrl = GetString(values, nameof(BugReportApiUrl), BugReportApiUrl);
        BugReportApiUrl = string.IsNullOrWhiteSpace(storedBugReportUrl)
            ? AppConstants.BugReportApiUrl
            : storedBugReportUrl;
        GoogleKey = GetSecret(values, nameof(GoogleKey), GoogleKey);
        UseMameDescriptions = GetBool(values, nameof(UseMameDescriptions), UseMameDescriptions);
        LastImageFolder = GetString(values, nameof(LastImageFolder), LastImageFolder);
        SupportedExtensions = GetStringList(values, nameof(SupportedExtensions)) ?? SupportedExtensions;
        AiAssistEnabled = GetBool(values, nameof(AiAssistEnabled), AiAssistEnabled);
        AiProvider = GetString(values, nameof(AiProvider), AiProvider);
        AiBaseUrl = GetString(values, nameof(AiBaseUrl), AiBaseUrl);
        AiApiKey = GetSecret(values, nameof(AiApiKey), AiApiKey);
        AiModel = GetString(values, nameof(AiModel), AiModel);
        AiRequestParameters = GetString(values, nameof(AiRequestParameters), AiRequestParameters);
        AiTimeoutSeconds = GetInt(values, nameof(AiTimeoutSeconds), AiTimeoutSeconds);
        AiMaxCandidates = GetInt(values, nameof(AiMaxCandidates), AiMaxCandidates);
        AiImageMaxDimension = GetInt(values, nameof(AiImageMaxDimension), AiImageMaxDimension);
        AiMinCoverWidth = GetInt(values, nameof(AiMinCoverWidth), AiMinCoverWidth);
        AiCandidateThreshold = GetDouble(values, nameof(AiCandidateThreshold), AiCandidateThreshold);
        AiAutoSaveThreshold = GetDouble(values, nameof(AiAutoSaveThreshold), AiAutoSaveThreshold);
        AiAutoSave = GetBool(values, nameof(AiAutoSave), AiAutoSave);
        AiAutoRun = GetBool(values, nameof(AiAutoRun), AiAutoRun);
        AiVerifyOnSave = GetBool(values, nameof(AiVerifyOnSave), AiVerifyOnSave);
        AiSkipPreviouslyQueried =
            GetBool(values, nameof(AiSkipPreviouslyQueried), AiSkipPreviouslyQueried);
    }

    private static string GetString(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        return values.GetValueOrDefault(key, fallback);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var value) &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> values, string key, double fallback)
    {
        return values.TryGetValue(key, out var value) &&
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static List<string>? GetStringList(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetSecret(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        if (!values.TryGetValue(key, out var stored) || string.IsNullOrEmpty(stored)) return fallback;

        var decrypted = UnprotectSecret(stored);
        return string.IsNullOrEmpty(decrypted) ? fallback : decrypted;
    }

    /// <summary>
    ///     Encrypts a secret for storage. Returns <see langword="null" /> when encryption
    ///     fails so callers can skip persisting the key instead of overwriting the stored
    ///     value with an empty string.
    /// </summary>
    /// <param name="secret">The plain-text secret; empty secrets return an empty string.</param>
    /// <returns>The protected secret, an empty string for an empty secret, or null on failure.</returns>
    private static string? ProtectSecretOrNull(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;

        try
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(secret)));
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Could not encrypt a settings secret; keeping the stored value.");
            return null;
        }
    }

    private static string UnprotectSecret(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret)) return string.Empty;

        try
        {
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(protectedSecret)));
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Could not decrypt a settings secret; the value was reset.");
            return string.Empty;
        }
    }

    private static SettingsData? LoadAndDecryptSettings(string filePath)
    {
        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            var decryptedBytes = Decrypt(encryptedBytes);
            var json = Encoding.UTF8.GetString(decryptedBytes);

            return JsonSerializer.Deserialize<SettingsData>(json);
        }
        catch (Exception ex)
        {
            // A corrupt or unreadable settings file is an environment issue (e.g. truncated
            // download, drive sync conflict or a machine rename invalidating the key),
            // not an application bug. Warn locally and let LoadSettings reset to defaults.
            LogService.WarningTransient(ex, $"Failed to decrypt settings from: {filePath}");
            return null;
        }
    }

    private bool TryMigrateFromLegacyXml()
    {
        var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
        if (!File.Exists(legacyPath))
            return false;

        try
        {
            var doc = XDocument.Load(legacyPath);
            var root = doc.Element("Settings");
            if (root == null) return false;

            var legacyData = new SettingsData
            {
                SimilarityThreshold = double.TryParse(
                    root.Element("SimilarityThreshold")?.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedThreshold)
                    ? parsedThreshold
                    : double.Parse(AppConstants.Messages.DefaultSimilarityThreshold, CultureInfo.InvariantCulture),
                SimilarityAlgorithm = AppConstants.Algorithms.JaroWinkler,
                BaseTheme = root.Element("BaseTheme")?.Value ?? AppConstants.Themes.Dark,
                AccentColor = root.Element("AccentColor")?.Value ?? "Blue",
                ImageWidth = int.TryParse(root.Element("ThumbnailSize")?.Value, CultureInfo.InvariantCulture, out var w)
                    ? w
                    : 300,
                ImageHeight =
                    int.TryParse(root.Element("ThumbnailSize")?.Value, CultureInfo.InvariantCulture, out var h)
                        ? h
                        : 300,
                MaxImagesToLoad = 30,
                ImageLoaderMaxRetries = 3,
                ImageLoaderRetryDelayMilliseconds = 200,
                ApiTimeoutSeconds = 30,
                SearchEngine = root.Element("SearchEngine")?.Value ?? "BingWeb",
                BugReportApiKey = root.Element("BugReportApiKey")?.Value is { Length: > 0 } bugReportKey
                    ? bugReportKey
                    : AppConstants.BugReportApiKey,
                BugReportApiUrl = root.Element("BugReportApiUrl")?.Value is { Length: > 0 } bugReportUrl
                    ? bugReportUrl
                    : AppConstants.BugReportApiUrl,
                GoogleKey = root.Element("GoogleKey")?.Value ?? string.Empty,
                UseMameDescriptions = bool.TryParse(root.Element("UseMameDescriptions")?.Value, out var useMame) &&
                                      useMame,
                LastImageFolder = string.Empty,
                SupportedExtensions = root.Element("SupportedExtensions")?
                    .Elements("Extension")
                    .Select(static x => x.Value)
                    .Where(static x => !string.IsNullOrWhiteSpace(x))
                    .ToList() ?? GetDefaultExtensions()
            };

            // Apply the migrated data and save it in the SQLite settings database
            SetDefaultSettings();
            ApplyData(legacyData);

            if (!SaveSettingsInternal())
            {
                // Keep the migrated values in memory for this session and leave the legacy
                // file in place so the migration is retried on the next start.
                LogService.WarningTransient(
                    "Could not persist migrated settings; keeping the legacy settings.xml file for a retry.");
                return true;
            }

            // Rename old file so we don't migrate again
            try
            {
                File.Move(legacyPath, legacyPath + ".migrated", true);
            }
            catch (Exception ex)
            {
                // Best effort - not critical
                LogService.Debug($"Could not rename migrated legacy settings file '{legacyPath}': {ex.Message}");
            }

            LogService.Information("Successfully migrated settings from settings.xml to Settings.dat");
            return true;
        }
        catch (Exception ex)
        {
            _ = ErrorLogger.LogAsync(ex, "Failed to migrate settings from settings.xml");
            return false;
        }
    }

    private List<string> GetLegacySettingsFilePaths()
    {
        var candidates = new[] { SettingsDatabasePath, LegacyAppDirSettingsFilePath }
            .Where(IsLegacySettingsFile)
            .ToList();

        if (candidates.Count == 0) return candidates;

        try
        {
            return candidates.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        }
        catch (Exception ex)
        {
            LogService.Debug($"Could not order legacy settings files by write time: {ex.Message}");
            return candidates;
        }
    }

    private static bool IsLegacySettingsFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            Span<byte> header = stackalloc byte[16];
            using var stream = File.OpenRead(filePath);
            var read = stream.Read(header);

            return read > 0 && !header[..read].SequenceEqual(SqliteHeader);
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"Could not inspect settings file: {filePath}");
            return false;
        }
    }

    private static string? BackupLegacyFile(string filePath)
    {
        if (filePath.EndsWith(".legacy", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            var backupPath = filePath + ".legacy";
            File.Move(filePath, backupPath, true);
            LogService.Information($"Backed up legacy settings file to: {backupPath}");
            return backupPath;
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"Failed to back up legacy settings file: {filePath}");
            return null;
        }
    }

    private void QuarantineSettingsFile()
    {
        foreach (var path in new[] { SettingsDatabasePath, LegacyAppDirSettingsFilePath })
        {
            if (!File.Exists(path)) continue;

            try
            {
                var corruptPath = path + ".corrupt";
                File.Move(path, corruptPath, true);
                LogService.WarningTransient($"Quarantined unreadable settings file to: {corruptPath}");
            }
            catch (Exception quarantineEx)
            {
                // Best effort - if we cannot move the file the defaults save below will
                // still overwrite it where permissions allow.
                LogService.WarningTransient(quarantineEx, $"Failed to quarantine unreadable settings file: {path}");
            }
        }
    }

    /// <summary>
    ///     Persists the current settings to the SQLite settings database.
    /// </summary>
    /// <returns><see langword="true" /> when the settings were persisted; otherwise <see langword="false" />.</returns>
    public bool SaveSettings()
    {
        lock (_ioLock)
        {
            return SaveSettingsInternal();
        }
    }

    private bool SaveSettingsInternal()
    {
        if (_databaseUnavailable)
        {
            LogService.WarningTransient(
                "Settings were not saved because the database was unavailable at startup; the existing file was left untouched.");
            ShowSaveError(
                "Settings could not be saved because the settings database was unavailable when the application started.\n\nThe existing settings file was left untouched; restart the application and try again.");
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(SimilarityThreshold)] = SimilarityThreshold.ToString(CultureInfo.InvariantCulture),
            [nameof(SelectedSimilarityAlgorithm)] = SelectedSimilarityAlgorithm,
            [nameof(IgnoreBracketedText)] = IgnoreBracketedText.ToString(CultureInfo.InvariantCulture),
            [nameof(BaseTheme)] = BaseTheme,
            [nameof(AccentColor)] = AccentColor,
            [nameof(ImageWidth)] = ImageWidth.ToString(CultureInfo.InvariantCulture),
            [nameof(ImageHeight)] = ImageHeight.ToString(CultureInfo.InvariantCulture),
            [nameof(MaxImagesToLoad)] = MaxImagesToLoad.ToString(CultureInfo.InvariantCulture),
            [nameof(ImageLoaderMaxRetries)] = ImageLoaderMaxRetries.ToString(CultureInfo.InvariantCulture),
            [nameof(ImageLoaderRetryDelayMilliseconds)] =
                ImageLoaderRetryDelayMilliseconds.ToString(CultureInfo.InvariantCulture),
            [nameof(ApiTimeoutSeconds)] = ApiTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [nameof(SearchEngine)] = SearchEngine,
            [nameof(BugReportApiUrl)] = BugReportApiUrl,
            [nameof(UseMameDescriptions)] = UseMameDescriptions.ToString(CultureInfo.InvariantCulture),
            [nameof(LastImageFolder)] = LastImageFolder,
            [nameof(SupportedExtensions)] = JsonSerializer.Serialize(SupportedExtensions),
            [nameof(AiAssistEnabled)] = AiAssistEnabled.ToString(CultureInfo.InvariantCulture),
            [nameof(AiProvider)] = AiProvider,
            [nameof(AiBaseUrl)] = AiBaseUrl,
            [nameof(AiModel)] = AiModel,
            [nameof(AiRequestParameters)] = AiRequestParameters,
            [nameof(AiTimeoutSeconds)] = AiTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [nameof(AiMaxCandidates)] = AiMaxCandidates.ToString(CultureInfo.InvariantCulture),
            [nameof(AiImageMaxDimension)] = AiImageMaxDimension.ToString(CultureInfo.InvariantCulture),
            [nameof(AiMinCoverWidth)] = AiMinCoverWidth.ToString(CultureInfo.InvariantCulture),
            [nameof(AiCandidateThreshold)] = AiCandidateThreshold.ToString(CultureInfo.InvariantCulture),
            [nameof(AiAutoSaveThreshold)] = AiAutoSaveThreshold.ToString(CultureInfo.InvariantCulture),
            [nameof(AiAutoSave)] = AiAutoSave.ToString(CultureInfo.InvariantCulture),
            [nameof(AiAutoRun)] = AiAutoRun.ToString(CultureInfo.InvariantCulture),
            [nameof(AiVerifyOnSave)] = AiVerifyOnSave.ToString(CultureInfo.InvariantCulture),
            [nameof(AiSkipPreviouslyQueried)] = AiSkipPreviouslyQueried.ToString(CultureInfo.InvariantCulture)
        };

        // Secret values are only written when encryption succeeds, so a transient
        // crypto failure cannot overwrite the stored ciphertext with an empty value.
        AddProtectedSecret(values, nameof(BugReportApiKey), BugReportApiKey);
        AddProtectedSecret(values, nameof(GoogleKey), GoogleKey);
        AddProtectedSecret(values, nameof(AiApiKey), AiApiKey);

        if (!new SettingsDatabase(SettingsDatabasePath).SaveAll(values))
        {
            ShowSaveError("Could not save settings to the settings database. Your settings changes may be lost.");
            return false;
        }

        return true;
    }

    private static void AddProtectedSecret(Dictionary<string, string> values, string key, string secret)
    {
        var protectedSecret = ProtectSecretOrNull(secret);
        if (protectedSecret != null) values[key] = protectedSecret;
    }

    private static void ShowSaveError(string message)
    {
        var reporter = ErrorReporter;
        if (reporter != null)
            reporter(message, "Settings Error");
        else
            _ = ErrorLogger.LogAsync(new InvalidOperationException(message), "Settings save error (no UI available)");
    }

    private void SetDefaultSettings()
    {
        _similarityThreshold =
            double.Parse(AppConstants.Messages.DefaultSimilarityThreshold, CultureInfo.InvariantCulture);
        _selectedSimilarityAlgorithm = AppConstants.Algorithms.JaroWinkler;
        _ignoreBracketedText = true;
        _supportedExtensions = GetDefaultExtensions();
        _imageWidth = 300;
        _imageHeight = 300;
        _maxImagesToLoad = 30;
        _imageLoaderMaxRetries = 3;
        _imageLoaderRetryDelayMilliseconds = 200;
        _apiTimeoutSeconds = 30;
        _baseTheme = AppConstants.Themes.Dark;
        _accentColor = "Blue";
        _useMameDescriptions = false;
        _lastImageFolder = string.Empty;
        _searchEngine = "BingWeb";
        _bugReportApiKey = AppConstants.BugReportApiKey;
        _bugReportApiUrl = AppConstants.BugReportApiUrl;
        _googleKey = string.Empty;
        _aiAssistEnabled = false;
        _aiProvider = AppConstants.AiProviders.OpenRouter;
        _aiBaseUrl = string.Empty;
        _aiApiKey = string.Empty;
        _aiModel = string.Empty;
        _aiRequestParameters = string.Empty;
        _aiTimeoutSeconds = 90;
        _aiMaxCandidates = 6;
        _aiImageMaxDimension = 512;
        _aiMinCoverWidth = 200;
        _aiAutoSaveThreshold = 80;
        _aiCandidateThreshold = 70;
        _aiAutoSave = false;
        _aiAutoRun = false;
        _aiVerifyOnSave = false;
        _aiSkipPreviouslyQueried = true;
    }

    private static List<string> GetDefaultExtensions()
    {
        return
        [
            "2hd", "3ds", "7z", "88d", "a78", "arc", "bat", "bin", "bs", "cas", "ccd", "cdi", "cdt", "chd", "cht",
            "ciso", "cmd", "col", "cpr", "cso", "cue", "cv", "d64", "d71", "d81", "d88", "dim", "dol", "dsk", "dup",
            "dummy", "elf", "exe", "fdi", "fds", "fig", "g64", "gb", "gcm", "gcz", "gdi", "gg", "gz", "hdf", "hdm",
            "img",
            "int", "ipf", "iso", "lnk", "lnx", "m3u", "mdf", "mds", "ms1", "msa", "mx1", "mx2", "n64", "nbz", "nca",
            "ndd", "nds", "nes", "nib", "nrg", "nro", "nso", "nsp", "o", "pbp", "pce", "prg", "prx", "rar", "ri",
            "rom", "rvz", "sc", "scl", "sda", "sf", "sfc", "sfx", "sg", "smc", "sms", "sna", "st", "stx", "swc",
            "t64", "tap", "tgc", "toc", "trd", "tzx", "u1", "unf", "unif", "url", "v64", "voc", "wad", "wbfs", "wua",
            "xci",
            "xdf", "z64", "z80", "zip", "zso",
            "gba", "gbc", "snes", "md", "smd", "gen", "32x", "sgg"
        ];
    }

    // --- Encryption/Decryption ---

    /// <summary>
    ///     Encrypts the specified bytes with the machine-derived AES key, prefixing the output with a random IV.
    /// </summary>
    /// <param name="data">The plaintext bytes to encrypt.</param>
    /// <returns>The IV-prefixed AES ciphertext.</returns>
    internal static byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Write IV first
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length < 16)
            throw new ArgumentException("Encrypted data is too short. Expected at least 16 bytes for the IV.",
                nameof(encryptedData));

        using var aes = Aes.Create();
        aes.Key = EncryptionKey;

        // Read IV from the beginning
        var iv = new byte[16];
        Array.Copy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();

        cs.CopyTo(result);
        return result.ToArray();
    }
}