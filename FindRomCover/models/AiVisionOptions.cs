namespace FindRomCover.Models;

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
    bool VerifyOnSave);
