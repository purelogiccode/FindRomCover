namespace FindRomCover.Models;

/// <summary>
///     Outcome of a single ROM processed by <see cref="Services.Ai.AiBatchFillService" />.
/// </summary>
public enum AiBatchOutcome
{
    /// <summary>A cover was saved from a locally matched image.</summary>
    FilledFromLocal,

    /// <summary>A cover was downloaded and saved from the web search API.</summary>
    FilledFromApi,

    /// <summary>No candidate reached the required confidence.</summary>
    SkippedLowConfidence,

    /// <summary>A cover already existed for the ROM.</summary>
    SkippedAlreadyExists,

    /// <summary>The ROM was already queried in a previous session.</summary>
    SkippedAlreadyQueried,

    /// <summary>No candidate images were found.</summary>
    NoCandidates,

    /// <summary>Processing the item failed.</summary>
    Failed,

    /// <summary>Processing was canceled before completion.</summary>
    Canceled
}