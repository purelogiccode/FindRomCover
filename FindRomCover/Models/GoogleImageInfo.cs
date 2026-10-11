namespace FindRomCover.Models;

/// <summary>
///     Metadata about an image returned by the Google Custom Search API.
/// </summary>
public sealed class GoogleImageInfo
{
    /// <summary>Image width in pixels.</summary>
    public int Width { get; set; }
    /// <summary>Image height in pixels.</summary>
    public int Height { get; set; }
    /// <summary>Image size in bytes.</summary>
    public int ByteSize { get; set; }
    /// <summary>URL of the image thumbnail, when provided by the API.</summary>
    public string? ThumbnailLink { get; set; }
}
