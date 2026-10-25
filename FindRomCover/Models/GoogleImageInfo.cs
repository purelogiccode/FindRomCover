namespace FindRomCover.Models;

/// <summary>
///     Metadata about an image returned by the Google Custom Search API.
/// </summary>
public sealed class GoogleImageInfo
{
    /// <summary>Image width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Image size in bytes.</summary>
    public int ByteSize { get; init; }

    /// <summary>URL of the image thumbnail, when provided by the API.</summary>
    public string? ThumbnailLink { get; init; }
}
