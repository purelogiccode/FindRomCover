namespace FindRomCover.Models;

public class SettingsData
{
    public double SimilarityThreshold { get; set; } = 70;
    public string SimilarityAlgorithm { get; set; } = "Jaro-Winkler Distance";
    public string BaseTheme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "Blue";
    public int ImageWidth { get; set; } = 300;
    public int ImageHeight { get; set; } = 300;
    public int MaxImagesToLoad { get; set; } = 30;
    public int ImageLoaderMaxRetries { get; set; } = 3;
    public int ImageLoaderRetryDelayMilliseconds { get; set; } = 200;
    public int ApiTimeoutSeconds { get; set; } = 30;
    public string SearchEngine { get; set; } = "BingWeb";
    public string BugReportApiKey { get; set; } = string.Empty;
    public string BugReportApiUrl { get; set; } = string.Empty;
    public string GoogleKey { get; set; } = string.Empty;
    public bool UseMameDescriptions { get; set; }
    public string LastImageFolder { get; set; } = string.Empty;
    public List<string> SupportedExtensions { get; set; } = [];

    public bool AiAssistEnabled { get; set; }
    public string AiProvider { get; set; } = AppConstants.AiProviders.OpenRouter;
    public string AiBaseUrl { get; set; } = string.Empty;
    public string AiApiKey { get; set; } = string.Empty;
    public string AiModel { get; set; } = string.Empty;
    public int AiTimeoutSeconds { get; set; } = 90;
    public int AiMaxCandidates { get; set; } = 6;
    public int AiImageMaxDimension { get; set; } = 512;
    public double AiAutoSaveThreshold { get; set; } = 80;
    public double AiCandidateThreshold { get; set; } = 70;
    public bool AiAutoSave { get; set; }
    public bool AiAutoRun { get; set; }
    public bool AiVerifyOnSave { get; set; }
    public bool AiSkipPreviouslyQueried { get; set; } = true;
}