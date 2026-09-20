namespace FindRomCover.Models;

public sealed class GoogleImageInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int ByteSize { get; set; }
    public string? ThumbnailLink { get; set; }
}