namespace FindRomCover.Models;

/// <summary>
///     Source of the candidate images used for an AI pick.
/// </summary>
public enum AiPickKind
{
    /// <summary>Candidates were matched from the local image folder.</summary>
    Local,

    /// <summary>Candidates were fetched from the web search API.</summary>
    ApiFallback
}