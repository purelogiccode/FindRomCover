using System.IO;
using System.Windows;
using ImageMagick;

namespace FindRomCover.Services;

public static class ImageProcessor
{
    public static void CleanupOrphanedTempFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        try
        {
            // Matches both temp naming schemes: the legacy "*.tmp" files and the
            // current "<output>.tmp<8 hex chars>" names used by local saves and downloads.
            var tempFiles = Directory.GetFiles(directoryPath, "*.tmp*");
            foreach (var tempFile in tempFiles)
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    LogService.Warning(ex, $"Failed to delete orphaned temp file: {tempFile}");
                }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, $"Failed to enumerate temp files in directory: {directoryPath}");
        }
    }

    public static Task<ImageSaveResult> ConvertAndSaveImageAsync(string sourcePath, string? targetPath,
        CancellationToken cancellationToken)
    {
        if (targetPath != null) return ConvertAndSaveImageCoreAsync(sourcePath, targetPath, cancellationToken);

        return Task.FromResult(new ImageSaveResult(false, "Target path is null.", "Error", MessageBoxImage.Error));
    }

    private static async Task<ImageSaveResult> ConvertAndSaveImageCoreAsync(string sourcePath, string targetPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (directory == null)
            return new ImageSaveResult(false, "Invalid target path.", "Error", MessageBoxImage.Error);

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            return new ImageSaveResult(false,
                "Source and target paths are the same.\n\nPlease choose another target path.", "Error",
                MessageBoxImage.Error);

        const int maxAttempts = 3;
        for (var attempt = 0;; attempt++)
            try
            {
                var testFile = Path.Combine(directory, $"{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testFile, string.Empty, cancellationToken);
                File.Delete(testFile);
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
                        MessageBoxImage.Error,
                        ex,
                        $"Cannot write to directory: {directory}");
            }

        cancellationToken.ThrowIfCancellationRequested();

        // Do NOT delete an existing target here: the conversion writes to a temp file
        // and only then replaces the target (see WriteImageWithRetryAsync), so a failed
        // conversion (corrupt source, OOM, locked file) never destroys the previous cover.
        return await ProcessImageAsync(sourcePath, targetPath, cancellationToken);
    }

    private static async Task<ImageSaveResult> ProcessImageAsync(string sourcePath, string targetPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return new ImageSaveResult(false, "Source image could not be found.", "Image Not Found",
                MessageBoxImage.Error);

        try
        {
            var settings = GetMagickReadSettings(sourcePath);
            using var magickImage = new MagickImage(sourcePath, settings);

            if (magickImage.Width == 0 || magickImage.Height == 0)
                throw new InvalidOperationException("Image has zero dimensions");

            magickImage.AutoOrient();
            magickImage.Quality = 90;
            magickImage.Format = MagickFormat.Png;

            return await WriteImageWithRetryAsync(magickImage, targetPath, sourcePath, cancellationToken);
        }
        catch (MagickException ex)
        {
            return new ImageSaveResult(false,
                $"Error processing image with Magick.NET: {ex.Message}",
                "Image Processing Error",
                MessageBoxImage.Error,
                ex,
                $"Magick.NET error: {sourcePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked/corrupt source file is environmental — return a failure result
            // instead of letting the exception reach the UI error handler.
            return new ImageSaveResult(false,
                $"Cannot access image file: {ex.Message}",
                "File Access Error",
                MessageBoxImage.Error,
                ex,
                $"File access error: {sourcePath}");
        }
    }

    private static async Task<ImageSaveResult> WriteImageWithRetryAsync(MagickImage magickImage, string targetPath,
        string sourcePath, CancellationToken cancellationToken)
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

                    File.Move(tempPath, targetPath, true);

                    return File.Exists(targetPath)
                        ? new ImageSaveResult(true)
                        : new ImageSaveResult(false,
                            "The image was processed but the target file was not created.",
                            "File Write Error",
                            MessageBoxImage.Error,
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
            MessageBoxImage.Error,
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

    internal static MagickReadSettings GetMagickReadSettings(string filePath)
    {
        var settings = new MagickReadSettings();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        settings.Format = ext switch
        {
            ".png" => MagickFormat.Png,
            ".jpg" or ".jpeg" => MagickFormat.Jpeg,
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

    public sealed record ImageSaveResult(
        bool Success,
        string? ErrorMessage = null,
        string? ErrorTitle = null,
        MessageBoxImage ErrorIcon = MessageBoxImage.None,
        Exception? Exception = null,
        string? LogContext = null);
}