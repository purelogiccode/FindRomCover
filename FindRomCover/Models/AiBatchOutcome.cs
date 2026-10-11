namespace FindRomCover.Models;

public enum AiBatchOutcome
{
    FilledFromLocal,
    FilledFromApi,
    SkippedLowConfidence,
    SkippedAlreadyExists,
    SkippedAlreadyQueried,
    NoCandidates,
    Failed,
    Canceled
}
