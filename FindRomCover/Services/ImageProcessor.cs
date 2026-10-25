using FindRomCover.Models;
using ImageMagick;

namespace FindRomCover.Services;

/// <summary>
///     ImageMagick-based helpers that clean up orphaned temporary files and convert
///     images to PNG so they can be saved for display.
/// </summary>
public static class ImageProcessor
{
    /// <summary>
    ///     Deletes leftover temporary files created by this application in the given directory.
    /// </summary>
    /// <param name="directoryPath">The directory to scan for orphaned temp files.</param>
    public static void CleanupOrphanedTempFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        try
        {
            // Only delete the app's own temp naming schemes: "*.tmp" and the current
            // "<output>.tmp<8 hex chars>" names. A broad "*.tmp*" pattern would also
            // delete user files whose names merely contain ".tmp".
            foreach (var tempFile in Directory.EnumerateFiles(directoryPath))
                if (IsOrphanedTempFileName(Path.GetFileName(tempFile)))
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception ex)
                    {
                        LogService.WarningTransient(ex, $"Failed to delete orphaned temp file: {tempFile}");
                    }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"Failed to enumerate temp files in directory: {directoryPath}");
        }
    }

    /// <summary>
    ///     Determines whether a file name matches one of the application's temporary naming schemes.
    /// </summary>
    /// <param name="fileName">The file name (without directory) to inspect.</param>
    /// <returns>True when the name looks like an orphaned temp file; otherwise false.</returns>
    internal static bool IsOrphanedTempFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;

        // Permission probe: "<32 hex chars>.tmp" and invalid-PNG normalization:
        // "<name>.<32 hex chars>.tmp". Only those app-owned names are removed; a
        // generic user file such as "backup.tmp" must never be deleted.
        if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            var stem = fileName[..^4];
            var lastDot = stem.LastIndexOf('.');
            var candidate = lastDot >= 0 ? stem[(lastDot + 1)..] : stem;
            return candidate.Length == 32 && candidate.All(char.IsAsciiHexDigit);
        }

        // Current temp scheme: "<output>.png.tmp<8 hex chars>". Requiring the .png
        // segment keeps a user file such as "cover.tmp20240101" (a date suffix is
        // valid hex) out of the cleanup.
        const string marker = ".png.tmp";
        var index = fileName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;

        var suffix = fileName[(index + marker.Length)..];
        return suffix.Length == 8 && suffix.All(char.IsAsciiHexDigit);
    }

    /// <summary>
    ///     Converts the source image to PNG and saves it at the target path.
    /// </summary>
    /// <param name="sourcePath">The path of the image to convert.</param>
    /// <param name="targetPath">The destination path, or null to return a failure result.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="overwrite">
    ///     When <see langword="false" /> an existing target file is never replaced; the save
    ///     fails instead. Batch jobs use this to protect covers that appeared while the AI
    ///     request was running.
    /// </param>
    /// <returns>The result of the conversion and save operation.</returns>
    public static Task<ImageSaveResult> ConvertAndSaveImageAsync(string sourcePath, string? targetPath,
        CancellationToken cancellationToken, bool overwrite = true)
    {
        if (targetPath != null)
            return ConvertAndSaveImageCoreAsync(sourcePath, targetPath, cancellationToken, overwrite);

        return Task.FromResult(new ImageSaveResult(false, "Target path is null.", "Error", MessageIcon.Error));
    }

    private static async Task<ImageSaveResult> ConvertAndSaveImageCoreAsync(string sourcePath, string targetPath,
        CancellationToken cancellationToken, bool overwrite)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (directory == null)
            return new ImageSaveResult(false, "Invalid target path.", "Error", MessageIcon.Error);

        if (AppConstants.FilePathComparer.Equals(sourcePath, targetPath))
            return new ImageSaveResult(false,
                "Source and target paths are the same.\n\nPlease choose another target path.", "Error",
                MessageIcon.Error);

        const int maxAttempts = 3;
        for (var attempt = 0; ; attempt++)
            try
            {
                var testFile = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
                // DeleteOnClose removes the probe even when a later explicit delete
                // would fail because of an antivirus/sync lock.
                await using var stream = new FileStream(testFile, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 1, FileOptions.DeleteOnClose);
                await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, cancellationToken);
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts - 1)
                    return new ImageSaveResult(
                        false,
                        $"Cannot write to directory: {directory}\n\nError: {ex.Message}\n\nTry running as administrator.",
                        "Permission Error",
                        MessageIcon.Error,
                        ex,
                        $"Cannot write to directory: {directory}");
            }

        cancellationToken.ThrowIfCancellationRequested();

        // Do NOT delete an existing target here: the conversion writes to a temp file
        // and only then replaces the target (see WriteImageWithRetryAsync), so a failed
        // conversion (corrupt source, OOM, locked file) never destroys the previous cover.
        return await ProcessImageAsync(sourcePath, targetPath, cancellationToken, overwrite);
    }

    private static async Task<ImageSaveResult> ProcessImageAsync(string sourcePath, string targetPath,
        CancellationToken cancellationToken, bool overwrite)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return new ImageSaveResult(false, "Source image could not be found.", "Image Not Found",
                MessageIcon.Error);

        MagickImage? magickImage = null;
        string? firstReadError = null;
        MagickException? readFailure = null;

        try
        {
            magickImage = new MagickImage(sourcePath, GetMagickReadSettings(sourcePath));
        }
        catch (MagickException ex)
        {
            firstReadError = ex.Message;
            readFailure = ex;
        }

        if (magickImage == null)
        {
            // Some files only decode with tolerant settings: truncated or partially written
            // PNGs (bad CRC) and files whose extension lies about the actual content. The
            // display path (ImageLoader) already tolerates these, so the save path must too
            // - otherwise a cover that displays correctly cannot be used (issues #68280,
            // #68282, #68283).
            LogService.Debug(
                $"ImageProcessor: strict read failed for '{sourcePath}': {firstReadError}. Retrying with tolerant settings.");
            try
            {
                magickImage = new MagickImage(sourcePath, GetTolerantMagickReadSettings());
            }
            catch (MagickException ex)
            {
                readFailure = ex;
            }
        }

        if (magickImage == null)
            return new ImageSaveResult(false,
                $"Error processing image with Magick.NET: {readFailure?.Message}",
                "Image Processing Error",
                MessageIcon.Error,
                readFailure,
                $"Magick.NET error for '{sourcePath}': {firstReadError} / tolerant retry: {readFailure?.Message}");

        try
        {
            using (magickImage)
            {
                if (magickImage.Width == 0 || magickImage.Height == 0)
                    return new ImageSaveResult(false,
                        "The image has zero width or height and cannot be processed.",
                        "Image Processing Error",
                        MessageIcon.Error,
                        new InvalidOperationException("Image has zero dimensions"),
                        $"Zero-dimension image: {sourcePath}");

                magickImage.AutoOrient();
                magickImage.Quality = 90;
                magickImage.Format = MagickFormat.Png;

                return await WriteImageWithRetryAsync(magickImage, targetPath, sourcePath, cancellationToken,
                    overwrite);
            }
        }
        catch (MagickException ex)
        {
            return new ImageSaveResult(false,
                $"Error processing image with Magick.NET: {ex.Message}",
                "Image Processing Error",
                MessageIcon.Error,
                ex,
                $"Magick.NET error for '{sourcePath}': {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked/corrupt source file is environmental — return a failure result
            // instead of letting the exception reach the UI error handler.
            return new ImageSaveResult(false,
                $"Cannot access image file: {ex.Message}",
                "File Access Error",
                MessageIcon.Error,
                ex,
                $"File access error for '{sourcePath}': {ex.Message}");
        }
    }

    private static async Task<ImageSaveResult> WriteImageWithRetryAsync(MagickImage magickImage, string targetPath,
        string sourcePath, CancellationToken cancellationToken, bool overwrite)
    {
        const int maxRetries = 5;
        const int baseDelayMs = 100;
        Exception? lastException = null;
        string? tempPath = null;

        try
        {
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Unique temp name per attempt: concurrent conversions of the same
                // target (double-click, AI auto-save racing a manual save) and
                // transient locks from AV/sync tools on a previous temp file must
                // never collide (issues #67436, #67437, #67438).
                tempPath = targetPath + ".tmp" + Guid.NewGuid().ToString("N")[..8];

                try
                {
                    await magickImage.WriteAsync(tempPath, magickImage.Format, cancellationToken);

                    if (!File.Exists(tempPath)) throw new IOException("Failed to write temporary file");

                    if (!overwrite && File.Exists(targetPath))
                    {
                        TryDeleteTempFile(tempPath);
                        return new ImageSaveResult(false,
                            "A cover with this name already exists and was not replaced.",
                            "Cover Exists",
                            MessageIcon.Warning,
                            null,
                            $"Refused to overwrite existing target: {targetPath}");
                    }

                    File.Move(tempPath, targetPath, overwrite);

                    return File.Exists(targetPath)
                        ? new ImageSaveResult(true)
                        : new ImageSaveResult(false,
                            "The image was processed but the target file was not created.",
                            "File Write Error",
                            MessageIcon.Error,
                            new IOException("Target file was not created"),
                            $"WriteBlob failed for: {sourcePath}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Catch on EVERY attempt, including the last: falling out of the
                    // loop must reach the friendly failure result below instead of
                    // letting the raw exception escape to the UI error handler.
                    lastException = ex;
                    TryDeleteTempFile(tempPath);

                    if (attempt >= maxRetries) break;

                    var delay = baseDelayMs * Math.Pow(2, attempt - 1);
                    await Task.Delay((int)delay, cancellationToken);
                }
            }
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }

        var errorMessage =
            $"Failed to save image after {maxRetries} attempts. The file may be locked by another process (e.g., OneDrive sync, antivirus).\n\nTarget: {targetPath}";
        if (lastException != null) errorMessage += $"\n\nLast error: {lastException.Message}";

        return new ImageSaveResult(
            false,
            errorMessage,
            "File Write Error",
            MessageIcon.Error,
            lastException ?? new IOException("Write failed after all retries"),
            $"WriteBlob failed after {maxRetries} retries for: {sourcePath}");
    }

    private static void TryDeleteTempFile(string? tempPath)
    {
        if (tempPath == null)
            return;

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (IOException)
        {
            // Best effort cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    ///     Builds Magick.NET read settings with the format matching the file's extension.
    /// </summary>
    /// <param name="filePath">The image file path whose extension selects the format.</param>
    /// <returns>The read settings for the given file.</returns>
    internal static MagickReadSettings GetMagickReadSettings(string filePath)
    {
        var settings = new MagickReadSettings();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        settings.Format = ext switch
        {
            ".png" or ".apng" => MagickFormat.Png,
            ".jpg" or ".jpeg" or ".jfif" or ".jpe" => MagickFormat.Jpeg,
            ".bmp" => MagickFormat.Bmp,
            ".gif" => MagickFormat.Gif,
            ".tiff" or ".tif" => MagickFormat.Tiff,
            ".ico" => MagickFormat.Ico,
            ".svg" => MagickFormat.Svg,
            ".webp" => MagickFormat.WebP,
            ".avif" => MagickFormat.Avif,
            ".heic" => MagickFormat.Heic,
            ".heif" => MagickFormat.Heif,
            ".jxl" => MagickFormat.Jxl,
            ".jp2" => MagickFormat.Jp2,
            _ => MagickFormat.Unknown
        };

        return settings;
    }

    /// <summary>
    ///     Determines whether the file is a complete, strictly readable PNG. A file named
    ///     <c>.png</c> that contains WebP/JPEG data or is truncated returns
    ///     <see langword="false"/> so the caller can normalize it through the tolerant
    ///     conversion path.
    /// </summary>
    /// <param name="filePath">The image file to inspect.</param>
    /// <returns><see langword="true"/> when the file is a valid PNG; otherwise <see langword="false"/>.</returns>
    internal static bool IsValidPng(string filePath)
    {
        try
        {
            using var image = new MagickImage(filePath, GetMagickReadSettings(filePath));
            return image is { Format: MagickFormat.Png, Width: > 0, Height: > 0 };
        }
        catch (MagickException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Builds tolerant Magick.NET read settings used as the fallback when the strict,
    ///     extension-based read fails. Mirrors <see cref="ImageLoader" />: the format is
    ///     auto-detected from the content (the extension may lie) and PNG CRC mismatches
    ///     (partially written or slightly damaged files) are ignored.
    /// </summary>
    /// <returns>The tolerant read settings.</returns>
    private static MagickReadSettings GetTolerantMagickReadSettings()
    {
        var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };
        settings.SetDefine(MagickFormat.Png, "ignore-crc", true);
        return settings;
    }
}
