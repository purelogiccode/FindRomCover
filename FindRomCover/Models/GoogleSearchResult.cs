namespace FindRomCover.Models;

/// <summary>
///     Top-level response of the Google Custom Search API.
/// </summary>
public sealed class GoogleSearchResult
{
    /// <summary>The returned image items, or <see langword="null" /> when the response contains none.</summary>
    public List<GoogleSearchItem>? Items { get; set; }
}