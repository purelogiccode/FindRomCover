namespace FindRomCover.Models;

/// <summary>
///     A single image result from the Google Custom Search API.
/// </summary>
public sealed class GoogleSearchItem
{
    /// <summary>Direct URL of the full-size image; empty when the API omitted it.</summary>
    public string Link { get; set; } = string.Empty;
    /// <summary>Additional metadata such as dimensions and thumbnail URL, when provided.</summary>
    public GoogleImageInfo? Image { get; set; }
    /// <summary>MIME type of the image; defaults to "Unknown".</summary>
    public string Mime { get; set; } = "Unknown";
    /// <summary>Image title used as the display name; defaults to "Unknown Filename".</summary>
    public string Title { get; set; } = "Unknown Filename";
}
