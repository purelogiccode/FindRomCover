namespace FindRomCover.Models;

public sealed record AiBatchItemResult(
    string RomName,
    string SearchName,
    AiBatchOutcome Outcome,
    string Message,
    string? SavedPath = null);
