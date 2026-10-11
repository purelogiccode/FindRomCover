using System.Reflection;
using System.Text;
using ImageMagick;

namespace FindRomCover.Services;

/// <summary>
///     Downloads images from URLs and converts streams to PNG covers, guarding against
///     oversized or non-image payloads.
/// </summary>
public static class ImageSaveService
{
    /// <summary>Maximum accepted download size in bytes; larger payloads are rejected.</summary>
    internal const int MaxDownloadBytes = 50 * 1024 * 1024;

    /// <summary>Browser-like User-Agent header used for image downloads.</summary>
    internal static readonly string BrowserUserAgent = BuildBrowserUserAgent();

    private static string BuildBrowserUserAgent()
    {
        var version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
        var versionText = version == null ? "0.0" : $"{version.Major}.{version.Minor}";

        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
               $"Chrome/131.0.0.0 Safari/537.36 FindRomCover/{versionText}";
    }

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
        return DownloadAndSaveImageAsync(imageUrl, null, outputPath, 0, cancellationToken);
    }

    /// <summary>
    ///     Downloads an image and saves it as PNG, falling back to a second URL (for example a
    ///     provider-hosted thumbnail) when the primary URL cannot be downloaded.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="fallbackImageUrl">An optional second URL tried when the primary download fails.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="minWidth">Minimum accepted image width in pixels; narrower images are rejected. 0 disables the check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    public static Task<bool> DownloadAndSaveImageAsync(string imageUrl, string? fallbackImageUrl, string outputPath,
        int minWidth, CancellationToken cancellationToken = default)
    {
        return DownloadAndSaveImageAsync(imageUrl, fallbackImageUrl, outputPath, minWidth,
            HttpClientHelper.Client, cancellationToken);
    }

    /// <summary>
    ///     Downloads an image and saves it as PNG using the supplied HTTP client, falling back to
    ///     a second URL when the primary download fails.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="fallbackImageUrl">An optional second URL tried when the primary download fails.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="httpClient">The HTTP client used for the download.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    internal static async Task<bool> DownloadAndSaveImageAsync(string imageUrl, string? fallbackImageUrl,
        string outputPath, HttpClient httpClient, CancellationToken cancellationToken)
    {
        return await DownloadAndSaveImageAsync(imageUrl, fallbackImageUrl, outputPath, 0, httpClient,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Downloads an image and saves it as PNG using the supplied HTTP client, falling back to
    ///     a second URL and rejecting images narrower than the minimum width.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="fallbackImageUrl">An optional second URL tried when the primary download fails.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="minWidth">Minimum accepted image width in pixels; narrower images are rejected. 0 disables the check.</param>
    /// <param name="httpClient">The HTTP client used for the download.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    internal static async Task<bool> DownloadAndSaveImageAsync(string imageUrl, string? fallbackImageUrl,
        string outputPath, int minWidth, HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            if (await TryDownloadAndSaveAsync(imageUrl, outputPath, minWidth, httpClient, cancellationToken)
                    .ConfigureAwait(false))
                return true;

            if (!string.IsNullOrWhiteSpace(fallbackImageUrl) &&
                !string.Equals(fallbackImageUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
            {
                LogService.WarningTransient($"Primary image download failed; trying fallback thumbnail '{fallbackImageUrl}'.");
                if (await TryDownloadAndSaveAsync(fallbackImageUrl, outputPath, minWidth, httpClient,
                            cancellationToken)
                        .ConfigureAwait(false))
                    return true;
            }

            LogService.WarningTransient($"Could not download image '{imageUrl}'.");
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

    /// <summary>
    ///     Attempts to download a single image URL and save it as PNG using the supplied HTTP client.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="httpClient">The HTTP client used for the download.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    internal static async Task<bool> TryDownloadAndSaveAsync(string imageUrl, string outputPath, HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        return await TryDownloadAndSaveAsync(imageUrl, outputPath, 0, httpClient, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Attempts to download a single image URL and save it as PNG, validating the payload and
    ///     rejecting images narrower than the minimum width.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="minWidth">Minimum accepted image width in pixels; narrower images are rejected. 0 disables the check.</param>
    /// <param name="httpClient">The HTTP client used for the download.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the download and save was successful, false otherwise.</returns>
    internal static async Task<bool> TryDownloadAndSaveAsync(string imageUrl, string outputPath, int minWidth,
        HttpClient httpClient, CancellationToken cancellationToken)
    {
        // Network-level failures (dead host, refused connection, reset, timeout,
        // malformed URL from search results) are environmental — return false so
        // the caller can try the fallback URL and so they never reach the
        // Error-level BugReportSink (issues #67379, #67382, #67383, #67432).
        try
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
            {
                LogService.WarningTransient($"Image download URL is not an absolute http(s) URI: '{imageUrl}'.");
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            request.Headers.Accept.ParseAdd(ImageAcceptHeader);

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogService.WarningTransient(
                    $"Image download returned {(int)response.StatusCode} ({response.StatusCode}) for '{imageUrl}'.");
                return false;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrEmpty(contentType) &&
                !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                LogService.WarningTransient(
                    $"Image download for '{imageUrl}' returned Content-Type '{contentType}' instead of image data; the host may be blocking automated requests.");
                return false;
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxDownloadBytes)
            {
                LogService.WarningTransient(
                    $"Image download for '{imageUrl}' reports {contentLength} bytes, exceeding the {MaxDownloadBytes}-byte limit; rejected.");
                return false;
            }

            await using var contentStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var bytes = await ReadAllBytesBoundedAsync(contentStream, MaxDownloadBytes, cancellationToken)
                .ConfigureAwait(false);
            if (bytes == null)
            {
                LogService.WarningTransient(
                    $"Image download for '{imageUrl}' exceeded the {MaxDownloadBytes}-byte limit; rejected.");
                return false;
            }

            if (bytes.Length == 0)
            {
                LogService.WarningTransient($"Image download for '{imageUrl}' returned an empty response body.");
                return false;
            }

            if (!LooksLikeImageData(bytes))
            {
                LogService.WarningTransient(
                    $"Image download for '{imageUrl}' returned {bytes.Length} bytes of non-image data (Content-Type '{contentType ?? "unknown"}'); the host may be blocking automated requests. Payload starts with: {PreviewPayload(bytes)}");
                return false;
            }

            await using var stream = new MemoryStream(bytes);
            return await ConvertStreamToPngAndSaveAsync(stream, outputPath, minWidth, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // HttpClient.Timeout fires with a token the caller never cancelled.
            LogService.WarningTransient(ex, $"Image download timed out for '{imageUrl}'.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            LogService.WarningTransient(ex, $"Image download failed for '{imageUrl}'.");
            return false;
        }
        catch (IOException ex)
        {
            LogService.WarningTransient(ex, $"Image download aborted for '{imageUrl}'.");
            return false;
        }
        catch (FormatException ex)
        {
            LogService.WarningTransient(ex, $"Image download URL is not valid: '{imageUrl}'.");
            return false;
        }
    }

    /// <summary>
    ///     Converts an image from a source stream to a PNG format at a destination path.
    ///     Preserves the aspect ratio, dimensions, and transparency using Magick.NET.
    /// </summary>
    /// <param name="inputStream">The stream containing the source image data.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if conversion was successful, false otherwise.</returns>
    public static Task<bool> ConvertStreamToPngAndSaveAsync(Stream inputStream, string outputPath,
        CancellationToken cancellationToken = default)
    {
        return ConvertStreamToPngAndSaveAsync(inputStream, outputPath, 0, cancellationToken);
    }

    /// <summary>
    ///     Converts an image from a source stream to a PNG format at a destination path.
    ///     Preserves the aspect ratio, dimensions, and transparency using Magick.NET.
    /// </summary>
    /// <param name="inputStream">The stream containing the source image data.</param>
    /// <param name="outputPath">The path where the PNG image will be saved.</param>
    /// <param name="minWidth">Minimum accepted image width in pixels; narrower images are rejected. 0 disables the check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if conversion was successful, false otherwise.</returns>
    public static async Task<bool> ConvertStreamToPngAndSaveAsync(Stream inputStream, string outputPath,
        int minWidth, CancellationToken cancellationToken = default)
    {
        var tempOutputPath = outputPath + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            LogService.Debug(
                $"Converting stream to PNG and saving to '{outputPath}' via temp file '{tempOutputPath}'.");

            var destDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            var bytes = await ReadAllBytesBoundedAsync(inputStream, MaxDownloadBytes, cancellationToken)
                .ConfigureAwait(false);
            if (bytes == null)
            {
                LogService.WarningTransient(
                    $"Stream exceeds the {MaxDownloadBytes}-byte limit; skipping conversion to '{outputPath}'.");
                return false;
            }

            if (!LooksLikeImageData(bytes))
            {
                LogService.WarningTransient(
                    $"Stream does not contain recognized image data ({bytes.Length} bytes); skipping conversion to '{outputPath}'.");
                return false;
            }

            using var imageStream = new MemoryStream(bytes);
            using (var image = new MagickImage(imageStream))
            {
                if (minWidth > 0 && image.Width < minWidth)
                {
                    LogService.WarningTransient(
                        $"Downloaded image is only {image.Width}x{image.Height}px, below the {minWidth}px minimum width; rejecting '{outputPath}'.");
                    return false;
                }

                image.Format = MagickFormat.Png;
                await image.WriteAsync(tempOutputPath, MagickFormat.Png, cancellationToken);
            }

            await MoveWithRetryAsync(tempOutputPath, outputPath).ConfigureAwait(false);
            LogService.Information($"Successfully saved image from stream to '{outputPath}'.");

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MagickMissingDelegateErrorException ex)
        {
            LogService.WarningTransient(ex, "Downloaded payload could not be decoded as an image.");
            return false;
        }
        catch (MagickException ex)
        {
            LogService.WarningTransient(ex, $"Downloaded payload could not be converted to PNG for '{outputPath}'.");
            return false;
        }
        catch (IOException ex)
        {
            // Locked/unwritable output is environmental — do not file bug reports.
            LogService.WarningTransient(ex, $"Could not write the converted image to '{outputPath}'.");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogService.WarningTransient(ex, $"Access denied writing the converted image to '{outputPath}'.");
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
                    LogService.WarningTransient(cleanupEx, $"Failed to clean up temporary file '{tempOutputPath}'.");
                }
        }
    }

    /// <summary>
    ///     Moves the converted temp file over the target, retrying transient locks
    ///     (antivirus, cloud-sync clients) with linear backoff.
    /// </summary>
    private static async Task MoveWithRetryAsync(string sourcePath, string targetPath)
    {
        const int maxAttempts = 5;
        const int baseDelayMs = 100;

        for (var attempt = 1;; attempt++)
            try
            {
                File.Move(sourcePath, targetPath, true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < maxAttempts)
            {
                await Task.Delay(baseDelayMs * attempt).ConfigureAwait(false);
            }
    }

    /// <summary>
    ///     Reads the stream fully but aborts (returns null) once <paramref name="maxBytes"/>
    ///     is exceeded, so a hostile or broken server can never OOM the process.
    /// </summary>
    internal static async Task<byte[]?> ReadAllBytesBoundedAsync(Stream stream, int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes) return null;
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Checks whether the first bytes of a payload match a known image file signature.
    /// </summary>
    /// <param name="bytes">The payload to inspect.</param>
    /// <returns>True when the payload starts with a recognized image signature; otherwise false.</returns>
    internal static bool LooksLikeImageData(byte[] bytes)
    {
        if (bytes.Length < 4) return false;

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;
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
