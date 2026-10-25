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
    /// <summary>
    ///     Calculates similarity for the images in a folder and loads the best matches for display.
    /// </summary>
    /// <param name="selectedRomFileName">The ROM file name to compare against.</param>
    /// <param name="imageFolderPath">The folder containing candidate images.</param>
    /// <param name="similarityThreshold">Minimum similarity percentage required for a match.</param>
    /// <param name="similarityAlgorithm">The algorithm used to score similarity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="onImageLoaded">Optional callback invoked for each successfully loaded image.</param>
    /// <param name="ignoreBracketedText">True to strip bracketed annotations from names before comparing.</param>
    /// <returns>The similarity result containing matched images and processing errors.</returns>
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

    /// <summary>
    ///     Builds or reuses the context menu for a cover image with actions to use, copy,
    ///     reveal, and open the image file.
    /// </summary>
    /// <param name="imagePath">The image file path the menu actions operate on.</param>
    /// <param name="useImageAction">Callback invoked when the user chooses "Use This Image".</param>
    /// <param name="existingMenu">An optional existing menu to reuse instead of creating a new one.</param>
    /// <returns>The context menu for the image.</returns>
    public static ContextMenu CreateContextMenu(string imagePath, Action<string?> useImageAction,
        ContextMenu? existingMenu = null)
    {
        if (string.IsNullOrEmpty(imagePath)) return new ContextMenu();

        if (existingMenu is { Items.Count: > 0 } && existingMenu.Tag is ContextMenuState existingState)
        {
            // The menu is recycled with its list container: retarget the shared state so
            // every action operates on the image that is actually displayed now. The
            // callback must be refreshed too — it captures the ROM selected when the
            // menu was last opened, which may have changed since.
            existingState.ImagePath = imagePath;
            existingState.UseAction = useImageAction;
            return existingMenu;
        }

        var contextMenu = existingMenu ?? new ContextMenu();
        if (contextMenu.Items.Count > 0)
            contextMenu.Items.Clear();

        var state = new ContextMenuState { ImagePath = imagePath, UseAction = useImageAction };
        contextMenu.Tag = state;

        var useThisImageMenuItem = new MenuItem
        {
            Header = "Use This Image",
            Icon = CreateIcon("usethis.png")
        };
        useThisImageMenuItem.Click += (_, _) => state.UseAction(state.ImagePath);
        contextMenu.Items.Add(useThisImageMenuItem);

        var copyMenuItem = new MenuItem
        {
            Header = "Copy Image Filename",
            Icon = CreateIcon("copy.png")
        };
        copyMenuItem.Click += async (_, _) => await CopyImageFilenameAsync(contextMenu, state.ImagePath);
        contextMenu.Items.Add(copyMenuItem);

        var openLocationMenuItem = new MenuItem
        {
            Header = "Open File Location",
            Icon = CreateIcon("folder.png")
        };
        openLocationMenuItem.Click += (_, _) => OpenFileLocation(state.ImagePath);
        contextMenu.Items.Add(openLocationMenuItem);

        var openFileMenuItem = new MenuItem
        {
            Header = "Open File",
            Icon = CreateIcon("usethis.png")
        };
        openFileMenuItem.Click += (_, _) => OpenFile(state.ImagePath);
        contextMenu.Items.Add(openFileMenuItem);

        return contextMenu;
    }

    private sealed class ContextMenuState
    {
        public string ImagePath { get; set; } = string.Empty;
        public Action<string?> UseAction { get; set; } = static _ => { };
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
            LogService.WarningTransient(ex, $"Could not load context menu icon '{fileName}'.");
            return new Image { Width = 16, Height = 16 };
        }
    }

    private static async Task CopyImageFilenameAsync(Control owner, string imagePath)
    {
        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);
        var window = TopLevel.GetTopLevel(owner) as Window;

        try
        {
            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard == null)
            {
                Dialogs.ShowMessage(window, "Could not copy to clipboard: no clipboard is available.",
                    "Clipboard Error", MessageIcon.Warning);
                return;
            }

            // Await the clipboard write: the task faults asynchronously when another
            // process owns the clipboard, which a synchronous try/catch cannot observe.
            await clipboard.SetTextAsync(filenameWithoutExtension);
            Dialogs.ShowMessage(window, $"Filename '{filenameWithoutExtension}' copied to clipboard!", "Copied");
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "Could not copy to clipboard.");
            Dialogs.ShowMessage(window,
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
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{sanitizedPath}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                using var process = Process.Start("open", ["-R", imagePath]);
            }
            else
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(imagePath) ?? ".",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"Could not open file location for '{imagePath}'.");
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
            LogService.WarningTransient(ex, $"Could not open file '{imagePath}'.");
        }
    }
}