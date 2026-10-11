using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FindRomCover.Managers;
using FindRomCover.Services;

namespace FindRomCover;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private readonly ObservableCollection<string> _supportedExtensions;

    public SettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();

        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));

        _supportedExtensions =
            new ObservableCollection<string>(
                _settingsManager.SupportedExtensions.OrderBy(static e => e, StringComparer.OrdinalIgnoreCase));
        DataContext = new { SupportedExtensions = _supportedExtensions };
    }

    private async void BtnAdd_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            var newExtension =
                await Dialogs.ShowInputAsync(this, "Add Extension", "Enter the new file extension (without the dot):");

            if (string.IsNullOrWhiteSpace(newExtension)) return;

            // Clean up the input
            newExtension = newExtension.Trim().Replace(".", "");

            // Validate the extension
            if (string.IsNullOrEmpty(newExtension))
            {
                await Dialogs.ShowMessageAsync(this, "Extension cannot be empty or just a dot.", "Invalid Input",
                    MessageIcon.Warning);
                return;
            }

            // Check for valid characters in file extension
            if (!IsValidExtension(newExtension))
            {
                await Dialogs.ShowMessageAsync(this,
                    "Extension contains invalid characters. Only letters, numbers, and hyphens are allowed.",
                    "Invalid Input", MessageIcon.Warning);
                return;
            }

            // Check for duplicates BEFORE normalization to catch any case variations
            if (_supportedExtensions.Contains(newExtension, StringComparer.OrdinalIgnoreCase))
            {
                await Dialogs.ShowMessageAsync(this,
                    $"The extension '{newExtension}' already exists (case-insensitive match).",
                    "Duplicate", MessageIcon.Warning);
                return;
            }

            // Normalize to lowercase for consistency
            newExtension = newExtension.ToLowerInvariant();

            _supportedExtensions.Add(newExtension);

            LstSupportedExtensions.SelectedItem = newExtension;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in method BtnAdd_ClickAsync");
        }
    }

    private static bool IsValidExtension(string extension)
    {
        // Check if extension contains only valid characters (letters, numbers, hyphens)
        // and is not empty
        if (string.IsNullOrEmpty(extension))
            return false;

        return extension.All(static c => char.IsLetterOrDigit(c) || c == '-');
    }

    private void BtnRemove_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (LstSupportedExtensions.SelectedItem is string selectedExtension)
                _supportedExtensions.Remove(selectedExtension);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnRemove_Click");
        }
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Validate all extensions before saving
            var invalidExtensions = _supportedExtensions
                .Where(static ext => !IsValidExtension(ext))
                .ToList();

            if (invalidExtensions.Count != 0)
            {
                var invalidList = string.Join(", ", invalidExtensions);
                var result = await Dialogs.ShowQuestionAsync(this,
                    $"The following extensions contain invalid characters: {invalidList}\n\n" +
                    "Only letters, numbers, and hyphens are allowed in file extensions.\n\n" +
                    "Would you like to remove these invalid extensions and save the rest?",
                    "Invalid Extensions", MessageIcon.Warning);

                if (result == MessageResult.Yes)
                {
                    // Remove invalid extensions
                    foreach (var invalid in invalidExtensions) _supportedExtensions.Remove(invalid);

                    // Re-sort the ObservableCollection to maintain UI consistency
                    var sortedExtensions = _supportedExtensions
                        .OrderBy(static ext => ext, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    _supportedExtensions.Clear();
                    foreach (var ext in sortedExtensions) _supportedExtensions.Add(ext);
                }
                else
                {
                    // User chose not to continue, exit without saving
                    return;
                }
            }

            // Ensure extensions are lowercase for consistency
            var normalizedExtensions = _supportedExtensions
                .Select(static ext => ext.ToLowerInvariant())
                .OrderBy(static ext => ext, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _settingsManager.SupportedExtensions = normalizedExtensions;
            _settingsManager.SaveSettings();

            Close(true);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnSave_Click");
            await Dialogs.ShowMessageAsync(this,
                "An error occurred while saving settings. Your changes were not saved.",
                "Error", MessageIcon.Error);
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
