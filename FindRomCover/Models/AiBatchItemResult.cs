namespace FindRomCover.Models;

/// <summary>
///     Result of processing a single ROM during an AI batch fill run.
/// </summary>
/// <param name="RomName">Name of the ROM the result belongs to.</param>
/// <param name="SearchName">Search name used to look for candidate images.</param>
/// <param name="Outcome">Outcome describing what happened to the item.</param>
/// <param name="Message">Human-readable detail about the outcome.</param>
/// <param name="SavedPath">Path of the saved cover image, or <see langword="null" /> when nothing was saved.</param>
public sealed record AiBatchItemResult(
    string RomName,
    string SearchName,
    AiBatchOutcome Outcome,
    string Message,
    string? SavedPath = null);
