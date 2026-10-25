namespace FindRomCover.Models;

/// <summary>
///     Persisted application settings.
/// </summary>
public class SettingsData
{
    /// <summary>Minimum similarity score (0-100) required for a local image match.</summary>
    public double SimilarityThreshold { get; init; } = 70;

    /// <summary>Name of the algorithm used to compare ROM and image names.</summary>
    public string SimilarityAlgorithm { get; init; } = "Jaro-Winkler Distance";

    /// <summary>Whether bracketed text is ignored when comparing names.</summary>
    public bool IgnoreBracketedText { get; init; } = true;

    /// <summary>Name of the base UI theme.</summary>
    public string BaseTheme { get; init; } = "Dark";

    /// <summary>Name of the UI accent color.</summary>
    public string AccentColor { get; init; } = "Blue";

    /// <summary>Default display width of cover images in pixels.</summary>
    public int ImageWidth { get; init; } = 300;

    /// <summary>Default display height of cover images in pixels.</summary>
    public int ImageHeight { get; init; } = 300;

    /// <summary>Maximum number of images loaded per search.</summary>
    public int MaxImagesToLoad { get; init; } = 30;

    /// <summary>Maximum number of retries when loading an image.</summary>
    public int ImageLoaderMaxRetries { get; init; } = 3;

    /// <summary>Delay in milliseconds between image loading retries.</summary>
    public int ImageLoaderRetryDelayMilliseconds { get; init; } = 200;

    /// <summary>Timeout in seconds for API requests.</summary>
    public int ApiTimeoutSeconds { get; init; } = 30;

    /// <summary>Selected web search provider.</summary>
    public string SearchEngine { get; init; } = "BingWeb";

    /// <summary>API key for the bug report service.</summary>
    public string BugReportApiKey { get; init; } = AppConstants.BugReportApiKey;

    /// <summary>API URL for the bug report service.</summary>
    public string BugReportApiUrl { get; init; } = AppConstants.BugReportApiUrl;

    /// <summary>Google Custom Search API key.</summary>
    public string GoogleKey { get; init; } = string.Empty;

    /// <summary>Whether MAME descriptions are used as search names.</summary>
    public bool UseMameDescriptions { get; init; }

    /// <summary>Last image folder selected by the user.</summary>
    public string LastImageFolder { get; init; } = string.Empty;

    /// <summary>File extensions treated as supported images.</summary>
    public List<string> SupportedExtensions { get; init; } = [];

    /// <summary>Whether AI assistance is enabled.</summary>
    public bool AiAssistEnabled { get; init; }

    /// <summary>Selected AI provider.</summary>
    public string AiProvider { get; init; } = AppConstants.AiProviders.OpenRouter;

    /// <summary>Base URL of the AI API.</summary>
    public string AiBaseUrl { get; init; } = string.Empty;

    /// <summary>API key for the AI provider.</summary>
    public string AiApiKey { get; init; } = string.Empty;

    /// <summary>Identifier of the AI model to use.</summary>
    public string AiModel { get; init; } = string.Empty;

    /// <summary>Optional JSON request parameters merged into AI provider requests.</summary>
    public string AiRequestParameters { get; init; } = string.Empty;

    /// <summary>Timeout in seconds for AI requests.</summary>
    public int AiTimeoutSeconds { get; init; } = 90;

    /// <summary>Maximum number of candidate images sent to the AI.</summary>
    public int AiMaxCandidates { get; init; } = 6;

    /// <summary>Maximum width and height (in pixels) for images sent to the AI.</summary>
    public int AiImageMaxDimension { get; init; } = 512;

    /// <summary>Minimum width in pixels required for a downloaded cover.</summary>
    public int AiMinCoverWidth { get; init; } = 200;

    /// <summary>Confidence percentage at or above which an AI pick is saved automatically.</summary>
    public double AiAutoSaveThreshold { get; init; } = 80;

    /// <summary>Minimum local similarity score (0-100) for an AI candidate.</summary>
    public double AiCandidateThreshold { get; init; } = 70;

    /// <summary>Whether confident AI picks are saved automatically.</summary>
    public bool AiAutoSave { get; init; }

    /// <summary>Whether the AI pick runs automatically after a search.</summary>
    public bool AiAutoRun { get; init; }

    /// <summary>Whether saved covers are verified by the AI.</summary>
    public bool AiVerifyOnSave { get; init; }

    /// <summary>Whether ROMs already queried in a previous session are skipped in batch runs.</summary>
    public bool AiSkipPreviouslyQueried { get; init; } = true;
}
