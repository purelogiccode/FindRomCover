namespace FindRomCover.Models;

/// <summary>
///     A downscaled, JPEG-encoded image prepared for upload to a vision model.
/// </summary>
/// <param name="Hash">Content hash used to cache and de-duplicate prepared images.</param>
/// <param name="JpegBytes">The encoded JPEG payload.</param>
public sealed record PreparedVisionImage(string Hash, byte[] JpegBytes);
