using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FindRomCover.Models;

namespace FindRomCover.Services;

/// <summary>
///     Avalonia replacement for the WPF ButtonFactory: builds the image context menu
///     and exposes the similarity collection builder used by the main window.
/// </summary>
public static class ButtonFactory
{
    public static Task<SimilarityCalculationResult> CreateSimilarImagesCollectionAsync(
        string selectedRomFileName,
        string imageFolderPath,
        double similarityThreshold,
        string similarityAlgorithm,
        CancellationToken cancellationToken,
        Action<ImageData>? onImageLoaded = null,
        bool ignoreBracketedText = false)
    {
        return SimilarityCalculator.CalculateSimilarityAsync(
            selectedRomFileName,
            imageFolderPath,
            similarityThreshold,
            similarityAlgorithm,
            cancellationToken,
            onImageLoaded: onImageLoaded,
            ignoreBracketedText: ignoreBracketedText);
    }

    public static ContextMenu CreateContextMenu(string imagePath, Action<string?> useImageAction,
        ContextMenu? existingMenu = null)
    {
        if (string.IsNullOrEmpty(imagePath)) return new ContextMenu();

        if (existingMenu is { Items.Count: > 0 })
        {
            foreach (var item in existingMenu.Items)
                if (item is MenuItem menuItem)
                    menuItem.CommandParameter = imagePath;

            return existingMenu;
        }

        var contextMenu = existingMenu ?? new ContextMenu();

        var useThisImageMenuItem = new MenuItem
        {
            Header = "Use This Image",
            Icon = CreateIcon("usethis.png")
        };
        useThisImageMenuItem.Click += (_, _) => useImageAction(imagePath);
        contextMenu.Items.Add(useThisImageMenuItem);

        var copyMenuItem = new MenuItem
        {
            Header = "Copy Image Filename",
            Icon = CreateIcon("copy.png")
        };
        copyMenuItem.Click += (_, _) => CopyImageFilename(contextMenu, imagePath);
        contextMenu.Items.Add(copyMenuItem);

        var openLocationMenuItem = new MenuItem
        {
            Header = "Open File Location",
            Icon = CreateIcon("folder.png")
        };
        openLocationMenuItem.Click += (_, _) => OpenFileLocation(imagePath);
        contextMenu.Items.Add(openLocationMenuItem);

        var openFileMenuItem = new MenuItem
        {
            Header = "Open File",
            Icon = CreateIcon("usethis.png")
        };
        openFileMenuItem.Click += (_, _) => OpenFile(imagePath);
        contextMenu.Items.Add(openFileMenuItem);

        return contextMenu;
    }

    private static Image CreateIcon(string fileName)
    {
        try
        {
            return new Image
            {
                Source = new Bitmap(AssetLoader.Open(
                    new Uri($"avares://FindRomCover/Assets/images/{fileName}"))),
                Width = 16,
                Height = 16,
                Margin = new Avalonia.Thickness(2)
            };
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"Could not load context menu icon '{fileName}'.");
            return new Image { Width = 16, Height = 16 };
        }
    }

    private static void CopyImageFilename(Control owner, string imagePath)
    {
        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);

        try
        {
            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard != null) _ = clipboard.SetTextAsync(filenameWithoutExtension);

            Dialogs.ShowMessage(TopLevel.GetTopLevel(owner) as Window,
                $"Filename '{filenameWithoutExtension}' copied to clipboard!",
                "Copied", MessageIcon.Information);
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Could not copy to clipboard.");
            Dialogs.ShowMessage(TopLevel.GetTopLevel(owner) as Window,
                "Could not copy to clipboard. It might be in use by another application.",
                "Clipboard Error", MessageIcon.Warning);
        }
    }

    private static void OpenFileLocation(string imagePath)
    {
        if (!File.Exists(imagePath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var sanitizedPath = imagePath.Replace("\"", "", StringComparison.Ordinal);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{sanitizedPath}\"",
                    UseShellExecute = true
                })?.Dispose();
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", ["-R", imagePath])?.Dispose();
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(imagePath) ?? ".",
                    UseShellExecute = true
                })?.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"Could not open file location for '{imagePath}'.");
        }
    }

    private static void OpenFile(string imagePath)
    {
        if (!File.Exists(imagePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = imagePath,
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"Could not open file '{imagePath}'.");
        }
    }
}
