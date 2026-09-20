namespace FindRomCover.Models;

public enum AiBatchOutcome
{
    FilledFromLocal,
    FilledFromApi,
    SkippedLowConfidence,
    SkippedAlreadyExists,
    NoCandidates,
    Failed,
    Canceled
}
