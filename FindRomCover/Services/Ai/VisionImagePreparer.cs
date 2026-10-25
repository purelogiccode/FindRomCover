using System.Security.Cryptography;
using FindRomCover.Models;
using ImageMagick;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Prepares images for vision model requests by resizing, stripping metadata, and re-encoding them as JPEG.
/// </summary>
public static class VisionImagePreparer
{
    /// <summary>
    ///     The default maximum width or height, in pixels, applied when preparing an image.
    /// </summary>
    private const int DefaultMaxDimension = 512;

    /// <summary>
    ///     The JPEG quality used when re-encoding prepared images.
    /// </summary>
    private const int JpegQuality = 80;

    /// <summary>
    ///     Prepares the image at the given file path for a vision request.
    /// </summary>
    /// <param name="filePath">The path of the image file to prepare.</param>
    /// <param name="maxDimension">The maximum width or height in pixels.</param>
    /// <returns>The prepared JPEG image and its hash.</returns>
    public static PreparedVisionImage Prepare(string filePath, int maxDimension = DefaultMaxDimension)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Image path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Image file was not found.", filePath);

        using var image = new MagickImage(filePath, ImageProcessor.GetMagickReadSettings(filePath));
        return PrepareCore(image, maxDimension);
    }

    /// <summary>
    ///     Prepares an image from raw bytes for a vision request.
    /// </summary>
    /// <param name="imageBytes">The raw image data.</param>
    /// <param name="maxDimension">The maximum width or height in pixels.</param>
    /// <returns>The prepared JPEG image and its hash.</returns>
    public static PreparedVisionImage Prepare(byte[] imageBytes, int maxDimension = DefaultMaxDimension)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("Image data is empty.", nameof(imageBytes));

        using var image = new MagickImage(imageBytes);
        return PrepareCore(image, maxDimension);
    }

    /// <summary>
    ///     Auto-orients, resizes, strips, and re-encodes the supplied image as a JPEG and hashes the result.
    /// </summary>
    /// <param name="image">The Magick.NET image to prepare.</param>
    /// <param name="maxDimension">The maximum width or height in pixels.</param>
    /// <returns>The prepared JPEG image and its SHA-256 hash.</returns>
    private static PreparedVisionImage PrepareCore(MagickImage image, int maxDimension)
    {
        maxDimension = maxDimension <= 0 ? DefaultMaxDimension : maxDimension;

        if (image.Width == 0 || image.Height == 0)
            throw new InvalidOperationException("Image has zero dimensions.");

        image.AutoOrient();

        if (image.Width > maxDimension || image.Height > maxDimension)
            image.Resize(new MagickGeometry((uint)maxDimension, (uint)maxDimension));

        image.Strip();
        image.Quality = JpegQuality;
        image.Format = MagickFormat.Jpeg;

        var jpegBytes = image.ToByteArray(MagickFormat.Jpeg);
        var hash = Convert.ToHexString(SHA256.HashData(jpegBytes));

        return new PreparedVisionImage(hash, jpegBytes);
    }
}
