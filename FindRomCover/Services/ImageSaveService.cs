using System.IO;
using System.Net.Http;
using System.Text;
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

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrEmpty(contentType) &&
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            LogService.Warning(
                $"Image download for '{imageUrl}' returned Content-Type '{contentType}' instead of image data; the host may be blocking automated requests.");
            return false;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            LogService.Warning($"Image download for '{imageUrl}' returned an empty response body.");
            return false;
        }

        if (!LooksLikeImageData(bytes))
        {
            LogService.Warning(
                $"Image download for '{imageUrl}' returned {bytes.Length} bytes of non-image data (Content-Type '{contentType ?? "unknown"}'); the host may be blocking automated requests. Payload starts with: {PreviewPayload(bytes)}");
            return false;
        }

        await using var stream = new MemoryStream(bytes);
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

            await using var buffer = new MemoryStream();
            await inputStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            var bytes = buffer.ToArray();
            if (!LooksLikeImageData(bytes))
            {
                LogService.Warning(
                    $"Stream does not contain recognized image data ({bytes.Length} bytes); skipping conversion to '{outputPath}'.");
                return false;
            }

            buffer.Position = 0;
            using (var image = new MagickImage(buffer))
            {
                image.Format = MagickFormat.Png;
                await image.WriteAsync(tempOutputPath, MagickFormat.Png, cancellationToken);
            }

            File.Move(tempOutputPath, outputPath, true);
            LogService.Information($"Successfully saved image from stream to '{outputPath}'.");

            return true;
        }
        catch (MagickMissingDelegateErrorException ex)
        {
            LogService.Warning(ex, "Downloaded payload could not be decoded as an image.");
            return false;
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

    internal static bool LooksLikeImageData(byte[] bytes)
    {
        if (bytes.Length < 4) return false;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        if (bytes[0] == 0xFF && (bytes[1] == 0xD8 || bytes[1] == 0x0A)) return true;
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return true;
        if (bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) return true;
        if (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A) return true;
        if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00) return true;

        if (bytes.Length >= 12)
        {
            if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true;
            if (bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70) return true;
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x0C &&
                bytes[4] == 0x4A && bytes[5] == 0x58 && bytes[6] == 0x4C && bytes[7] == 0x20) return true;
        }

        return IsSvg(bytes);
    }

    private static bool IsSvg(byte[] bytes)
    {
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var length = Math.Min(bytes.Length - offset, 1024);
        var text = Encoding.ASCII.GetString(bytes, offset, length).TrimStart();
        return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string PreviewPayload(byte[] bytes)
    {
        var length = Math.Min(bytes.Length, 160);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var c = (char)bytes[i];
            builder.Append(char.IsControl(c) ? '.' : c);
        }

        return builder.ToString();
    }
}
