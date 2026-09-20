using System.IO;
using System.Security.Cryptography;
using FindRomCover.Models;
using ImageMagick;

namespace FindRomCover.Services.Ai;

public static class VisionImagePreparer
{
    public const int DefaultMaxDimension = 512;
    public const int JpegQuality = 80;

    public static PreparedVisionImage Prepare(string filePath, int maxDimension = DefaultMaxDimension)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Image path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Image file was not found.", filePath);

        using var image = new MagickImage(filePath, ImageProcessor.GetMagickReadSettings(filePath));
        return PrepareCore(image, maxDimension);
    }

    public static PreparedVisionImage Prepare(byte[] imageBytes, int maxDimension = DefaultMaxDimension)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            throw new ArgumentException("Image data is empty.", nameof(imageBytes));

        using var image = new MagickImage(imageBytes);
        return PrepareCore(image, maxDimension);
    }

    internal static PreparedVisionImage PrepareCore(MagickImage image, int maxDimension)
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
