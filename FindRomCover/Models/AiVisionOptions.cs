namespace FindRomCover.Models;

/// <summary>
///     Immutable snapshot of the AI vision settings used for a request.
/// </summary>
/// <param name="Enabled">Whether AI assistance is enabled.</param>
/// <param name="Provider">Name of the AI provider.</param>
/// <param name="BaseUrl">Base URL of the vision API.</param>
/// <param name="ApiKey">API key used to authenticate with the provider.</param>
/// <param name="Model">Identifier of the model to use.</param>
/// <param name="TimeoutSeconds">Request timeout in seconds.</param>
/// <param name="MaxCandidates">Maximum number of candidate images sent to the model.</param>
/// <param name="ImageMaxDimension">Maximum width and height (in pixels) images are downscaled to.</param>
/// <param name="AutoSaveThreshold">Confidence percentage at or above which a pick may be auto-saved.</param>
/// <param name="AutoSave">Whether confident picks are saved automatically.</param>
/// <param name="AutoRun">Whether the AI pick runs automatically after a search.</param>
/// <param name="VerifyOnSave">Whether saved covers are verified by the AI.</param>
/// <param name="CandidateThreshold">Minimum local similarity score (0-100) for a candidate to be sent to the model.</param>
public sealed record AiVisionOptions(
    bool Enabled,
    string Provider,
    string BaseUrl,
    string ApiKey,
    string Model,
    int TimeoutSeconds,
    int MaxCandidates,
    int ImageMaxDimension,
    double AutoSaveThreshold,
    bool AutoSave,
    bool AutoRun,
    bool VerifyOnSave,
    double CandidateThreshold);
