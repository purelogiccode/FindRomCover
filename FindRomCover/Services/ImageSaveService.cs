using System.IO;
using System.Net.Http;
using ImageMagick;

namespace FindRomCover.Services;

public static class ImageSaveService
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 FindRomCover/3.2";

    private const string ImageAcceptHeader = "image/avif,image/webp,image/apng,image/*,*/*;q=0.8";

    /// <summary>
    ///     Downloads an image from the given URL and saves it as a PNG file at the specified path.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    public static Task<bool> DownloadAndSaveImageAsync(string imageUrl, string outputPath,
        CancellationToken cancellationToken = default)
    {
        return DownloadAndSaveImageAsync(imageUrl, null, outputPath, cancellationToken);
    }

    /// <summary>
    ///     Downloads an image and saves it as PNG, falling back to a second URL (for example a
    ///     provider-hosted thumbnail) when the primary URL cannot be downloaded.
    /// </summary>
    public static Task<bool> DownloadAndSaveImageAsync(string imageUrl, string? fallbackImageUrl, string outputPath,
        CancellationToken cancellationToken = default)
    {
        return DownloadAndSaveImageAsync(imageUrl, fallbackImageUrl, outputPath, HttpClientHelper.Client,
            cancellationToken);
    }

    internal static async Task<bool> DownloadAndSaveImageAsync(string imageUrl, string? fallbackImageUrl,
        string outputPath, HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            if (await TryDownloadAndSaveAsync(imageUrl, outputPath, httpClient, cancellationToken).ConfigureAwait(false))
                return true;

            if (!string.IsNullOrWhiteSpace(fallbackImageUrl) &&
                !string.Equals(fallbackImageUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Warning($"Primary image download failed; trying fallback thumbnail '{fallbackImageUrl}'.");
                if (await TryDownloadAndSaveAsync(fallbackImageUrl, outputPath, httpClient, cancellationToken)
                        .ConfigureAwait(false))
                    return true;
            }

            LogService.Warning($"Could not download image '{imageUrl}'.");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error downloading and saving image.");
            return false;
        }
    }

    internal static async Task<bool> TryDownloadAndSaveAsync(string imageUrl, string outputPath, HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
        request.Headers.Accept.ParseAdd(ImageAcceptHeader);

        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            LogService.Warning(
                $"Image download returned {(int)response.StatusCode} ({response.StatusCode}) for '{imageUrl}'.");
            return false;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await ConvertStreamToPngAndSaveAsync(stream, outputPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts an image from a source stream to a PNG format at a destination path.
    ///     Preserves the aspect ratio, dimensions, and transparency using Magick.NET.
    /// </summary>
    /// <param name="inputStream">The stream containing the source image data.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if conversion was successful, false otherwise.</returns>
    public static async Task<bool> ConvertStreamToPngAndSaveAsync(Stream inputStream, string outputPath,
        CancellationToken cancellationToken = default)
    {
        var tempOutputPath = outputPath + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            LogService.Debug(
                $"Converting stream to PNG and saving to '{outputPath}' via temp file '{tempOutputPath}'.");

            var destDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            using (var image = new MagickImage(inputStream))
            {
                image.Format = MagickFormat.Png;
                await image.WriteAsync(tempOutputPath, MagickFormat.Png, cancellationToken);
            }

            File.Move(tempOutputPath, outputPath, true);
            LogService.Information($"Successfully saved image from stream to '{outputPath}'.");

            return true;
        }
        catch (Exception ex)
        {
            // Log the error - UI notifications should be handled by the caller
            LogService.Error(ex, "General error during image conversion.");

            return false;
        }
        finally
        {
            if (File.Exists(tempOutputPath))
                try
                {
                    File.Delete(tempOutputPath);
                }
                catch (Exception cleanupEx)
                {
                    LogService.Warning(cleanupEx, $"Failed to clean up temporary file '{tempOutputPath}'.");
                }
        }
    }
}
