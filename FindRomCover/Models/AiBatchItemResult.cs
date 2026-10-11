namespace FindRomCover.Models;

/// <summary>
///     Result of processing a single ROM during an AI batch fill run.
/// </summary>
/// <param name="RomName">Name of the ROM the result belongs to.</param>
/// <param name="SearchName">Search name used to look for candidate images.</param>
/// <param name="Outcome">Outcome describing what happened to the item.</param>
/// <param name="Message">Human-readable detail about the outcome.</param>
/// <param name="SavedPath">Path associated with the outcome: the saved cover for filled results, the existing cover for skipped results, or the intended target for other outcomes; <see langword="null" /> when no path applies.</param>
public sealed record AiBatchItemResult(
    string RomName,
    string SearchName,
    AiBatchOutcome Outcome,
    string Message,
    string? SavedPath = null);