using FindRomCover.Managers;
using ImageMagick;

namespace FindRomCover.Services;

/// <summary>
///     Loads image files with Magick.NET, retrying transient lock errors (antivirus,
///     cloud sync) and normalizing the result to PNG bytes.
/// </summary>
public static class ImageLoader
{
    /// <summary>Default number of load attempts before giving up.</summary>
    public const int DefaultMaxRetries = 3;

    /// <summary>Default delay, in milliseconds, between load attempts.</summary>
    public const int DefaultRetryDelayMilliseconds = 200;

    /// <summary>
    ///     Loads and normalizes an image file to PNG bytes. UI heads convert the bytes
    ///     to their own bitmap type via <see cref="ImageSources.FromPngBytes" />.
    /// </summary>
    public static async Task<byte[]?> LoadImageToMemoryAsync(
        string? imagePath,
        CancellationToken cancellationToken,
        int maxRetries = 0,
        int retryDelayMilliseconds = 0)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;

        if (maxRetries <= 0 || retryDelayMilliseconds <= 0)
            (maxRetries, retryDelayMilliseconds) = ResolveSettings(maxRetries, retryDelayMilliseconds);

        var fileInfo = new FileInfo(imagePath);
        if (!fileInfo.Exists || fileInfo.Length == 0) return null;

        for (var i = 0; i < maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return LoadWithMagickNetInternal(imagePath);
            }
            catch (MagickException ex)
            {
                try
                {
                    return LoadWithMagickNetInternal(imagePath, true);
                }
                catch (Exception retryEx)
                {
                    LogService.Debug($"Could not load image '{imagePath}': {retryEx.Message} (first error: {ex.Message})");

                    if (i < maxRetries - 1)
                        await Task.Delay(retryDelayMilliseconds, cancellationToken);
                    else
                        return null;
                }
            }
            catch (IOException ex) when ((uint)ex.HResult is 0x80070020 or 0x80070021)
            {
                if (i < maxRetries - 1)
                    await Task.Delay(retryDelayMilliseconds, cancellationToken);
                else
                    return null;
            }
            catch (Exception ex)
            {
                LogService.Debug($"Unexpected error loading image '{imagePath}': {ex.Message}");
                return null;
            }
        }

        return null;
    }

    private static (int MaxRetries, int RetryDelayMilliseconds) ResolveSettings(int maxRetries,
        int retryDelayMilliseconds)
    {
        try
        {
            var settings = SettingsManager.CurrentInstance;
            var resolvedMaxRetries = maxRetries > 0 ? maxRetries : settings?.ImageLoaderMaxRetries ?? DefaultMaxRetries;
            var resolvedRetryDelay = retryDelayMilliseconds > 0
                ? retryDelayMilliseconds
                : settings?.ImageLoaderRetryDelayMilliseconds ?? DefaultRetryDelayMilliseconds;

            return (
                Math.Max(1, resolvedMaxRetries),
                Math.Max(0, resolvedRetryDelay));
        }
        catch (Exception ex)
        {
            LogService.Debug($"Could not resolve image loader settings: {ex.Message}");
            return (
                maxRetries > 0 ? maxRetries : DefaultMaxRetries,
                retryDelayMilliseconds > 0 ? retryDelayMilliseconds : DefaultRetryDelayMilliseconds);
        }
    }

    private static byte[] LoadWithMagickNetInternal(string imagePath, bool ignoreErrors = false)
    {
        var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };

        if (ignoreErrors) settings.SetDefine(MagickFormat.Png, "ignore-crc", true);

        using var magickImage = new MagickImage(imagePath, settings);

        if (magickImage.Width == 0 || magickImage.Height == 0)
            throw new InvalidOperationException($"Image has zero dimensions: {imagePath}");

        magickImage.AutoOrient();

        using var memoryStream = new MemoryStream();
        magickImage.Write(memoryStream, MagickFormat.Png);

        return memoryStream.ToArray();
    }
}