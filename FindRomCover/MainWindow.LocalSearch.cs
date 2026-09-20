using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

public partial class MainWindow
{
    private CancellationTokenSource? _webSearchCts;

    private void LstMissingImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_aiBatchRunning) return;

            CommandManager.InvalidateRequerySuggested();

            // Cancel any pending searches
            _findSimilarCts?.Cancel();
            _findSimilarCts?.Dispose();
            _findSimilarCts = null;
            _webSearchCts?.Cancel();
            _webSearchCts?.Dispose();
            _webSearchCts = null;
            _aiAssistCts?.Cancel();
            _aiAssistCts?.Dispose();
            _aiAssistCts = null;

            if (LstMissingImages.SelectedItem is not MissingImageItem selectedItem)
            {
                SimilarImages.Clear();
                PanelImages.Clear();
                LblLocalSearchQuery.Content = null;
                LblApiSearchQuery.Content = null;
                IsFindingSimilar = false;
                IsSearching = false;
                HasSearchedSimilar = false;
                HasSearchedApi = false;
                return;
            }

            var selectedItemRomName = selectedItem.RomName;
            var selectedItemSearchName = selectedItem.SearchName;
            _selectedRomFileName = selectedItemRomName;
            _imageFolderWatcher?.PendingRenameTarget = selectedItemRomName;

            try
            {
                Clipboard.SetText(selectedItemRomName);
            }
            catch (COMException)
            {
                // Clipboard may be locked by another process
            }

            // Always do local search
            _findSimilarTask = RunLocalSearchAsync(selectedItemSearchName, selectedItemRomName);
            _ = _findSimilarTask;

            TriggerActiveTabSearch(selectedItemSearchName);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in LstMissingImages_SelectionChanged");
        }
    }

    private void TriggerActiveTabSearch(string searchName)
    {
        var activeTab = SearchTabControl.SelectedIndex;

        // Build the search query for web/API tabs
        var extraQuery = TxtExtraQuery.Text.Trim();
        var cleanedSearchName = SearchQueryHelper.CleanSearchQuery(searchName);
        var searchQuery = !string.IsNullOrWhiteSpace(extraQuery)
            ? $"\"{cleanedSearchName}\" {extraQuery}"
            : $"\"{cleanedSearchName}\"";

        // Dispatch based on active tab
        switch (activeTab)
        {
            case 1: // Google Web
                IsSearching = true;
                StatusMessage.Text = "Searching Google...";
                _ = HandleGoogleWebSearchAsync(searchQuery);
                break;
            case 2: // Bing Web
                IsSearching = true;
                StatusMessage.Text = "Searching Bing...";
                _ = HandleBingWebSearchAsync(searchQuery);
                break;
            case 3: // Google API
                if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
                {
                    MessageBox.Show(
                        "A Google API key is required to use the Google API search.\n\nPlease enter your API key in the settings window that will open.",
                        "API Key Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    var apiSettingsWindow = new ApiSettingsWindow(Settings) { Owner = GetWindow(this) };
                    apiSettingsWindow.ShowDialog();

                    if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
                    {
                        StatusMessage.Text = "API key not set. Google API search is unavailable.";
                        IsSearching = false;
                        break;
                    }
                }

                IsSearching = true;
                StatusMessage.Text = "Searching Google API...";
                _webSearchCts = new CancellationTokenSource();
                LblApiSearchQuery.Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run("API search for: ") { FontWeight = FontWeights.Normal },
                        new Run(searchQuery) { FontWeight = FontWeights.Bold }
                    }
                };
                _ = HandleApiSearchAsync(searchQuery, _webSearchCts.Token);
                break;
        }
    }

    private async Task RunLocalSearchAsync(string searchName, string romName)
    {
        var imageFolderPath = GetValidatedImageFolderPath();
        if (string.IsNullOrEmpty(imageFolderPath))
        {
            IsFindingSimilar = false;
            return;
        }

        if (_findSimilarCts != null)
        {
            await _findSimilarCts.CancelAsync();
            _findSimilarCts.Dispose();
            _findSimilarCts = null;
        }

        _findSimilarCts = new CancellationTokenSource();
        var cancellationToken = _findSimilarCts.Token;

        IsFindingSimilar = true;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested) return;

            var textBlock = new TextBlock();
            textBlock.Inlines.Add(new Run("Search Query: "));
            textBlock.Inlines.Add(new Run($"{searchName} ") { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(new Run("for ROM: "));
            textBlock.Inlines.Add(new Run($"{romName} ") { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(new Run("with "));
            textBlock.Inlines.Add(new Run($"{Settings.SelectedSimilarityAlgorithm} ")
                { FontWeight = FontWeights.Bold });
            textBlock.Inlines.Add(new Run("algorithm"));
            LblLocalSearchQuery.Content = textBlock;

            SimilarImages.Clear();
        });

        try
        {
            await _findSimilarSemaphore.WaitAsync(cancellationToken);
            try
            {
                SimilarityCalculationResult similarityResult;
                try
                {
                    similarityResult = await ButtonFactory.CreateSimilarImagesCollectionAsync(
                        searchName,
                        imageFolderPath,
                        Settings.SimilarityThreshold,
                        Settings.SelectedSimilarityAlgorithm,
                        cancellationToken,
                        imageData =>
                        {
                            _ = Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    SimilarImages.Add(imageData);
                                    HasSearchedSimilar = true;
                                }
                            });
                        }
                    );
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                        await Application.Current.Dispatcher.InvokeAsync(static () => { });
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    HasSearchedSimilar = true;

                    if (similarityResult.ProcessingErrors.Count > 0)
                    {
                        var errorSummary =
                            $"Encountered {similarityResult.ProcessingErrors.Count} issues while processing images:\n\n";
                        errorSummary += string.Join("\n", similarityResult.ProcessingErrors.Take(5));
                        if (similarityResult.ProcessingErrors.Count > 5)
                            errorSummary += $"\n...and {similarityResult.ProcessingErrors.Count - 5} more.";

                        MessageBox.Show(errorSummary, "Image Processing Warnings", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    LocalImageScrollViewer.ScrollToTop();

                    if (Settings.AiAutoRun && Settings.AiAssistEnabled && SimilarImages.Count > 0)
                        _ = RunAiPickAsync();
                }
            }
            finally
            {
                _findSimilarSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                MessageBox.Show($"Error searching for similar images: {ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                LogService.Error(ex, "Error in RunLocalSearchAsync");
            }
        }
        finally
        {
            IsFindingSimilar = false;
        }
    }

    private async void BtnAiPickLocal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunAiPickAsync();
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Error in BtnAiPickLocal_Click");
        }
    }

    private async Task RunAiPickAsync()
    {
        if (!Settings.AiAssistEnabled)
        {
            MessageBox.Show(
                "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                "AI Assist", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (LstMissingImages.SelectedItem is not MissingImageItem selectedItem) return;
        if (SimilarImages.Count == 0 || IsAiBusy) return;

        _aiAssistCts?.Cancel();
        _aiAssistCts?.Dispose();
        _aiAssistCts = new CancellationTokenSource();
        var cancellationToken = _aiAssistCts.Token;

        _aiAssistService ??= new AiAssistService(Settings);

        var threshold = Settings.AiCandidateThreshold;
        var candidates = SimilarImages
            .Where(image => image.SimilarityScore >= threshold)
            .ToList();

        if (candidates.Count == 0)
        {
            StatusMessage.Text = $"No local images at or above the AI similarity threshold ({threshold:0}%).";
            return;
        }

        IsAiBusy = true;
        StatusMessage.Text = $"AI is analyzing {candidates.Count} candidate(s)...";

        try
        {
            var result = await _aiAssistService.PickBestAsync(
                selectedItem.RomName,
                selectedItem.SearchName,
                candidates,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (result is null)
            {
                StatusMessage.Text = "AI did not analyze any candidate image.";
                return;
            }

            MarkAiQuery(selectedItem);

            if (!result.HasPick || result.BestIndex < 0 || result.BestIndex >= candidates.Count)
            {
                StatusMessage.Text = $"AI found no genuine cover among the candidates. {result.Reason}".Trim();
                return;
            }

            foreach (var image in candidates) image.AiBadge = string.Empty;

            var picked = candidates[result.BestIndex];
            picked.AiBadge = $"AI pick {result.Confidence:P0}";

            var currentIndex = SimilarImages.IndexOf(picked);
            if (currentIndex > 0) SimilarImages.Move(currentIndex, 0);

            LocalImageScrollViewer.ScrollToTop();
            StatusMessage.Text = $"AI picked '{picked.ImageName}' ({result.Confidence:P0}). {result.Reason}".Trim();

            if (Settings.AiAutoSave && result.Confidence * 100 >= Settings.AiAutoSaveThreshold &&
                picked.ImagePath != null)
                await UseImageAsync(picked.ImagePath);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI assist failed");
            StatusMessage.Text = "AI assist failed. Check the log for details.";
            MessageBox.Show(ex.Message, "AI Assist", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    private void MarkAiQuery(MissingImageItem item)
    {
        try
        {
            var imageFolderPath = GetValidatedImageFolderPath(false);
            if (string.IsNullOrEmpty(imageFolderPath)) return;

            _aiQueryHistory ??= new AiQueryHistory();
            var targetPath = Path.Combine(imageFolderPath, SearchQueryHelper.SanitizeFileName(item.RomName) + ".png");
            _aiQueryHistory.MarkQueried(targetPath, "manual");
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI assist: could not record the query in the history.");
        }
    }

    private void ImageCell_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ImageData { ImagePath: not null } imageData })
                _ = UseImageAsync(imageData.ImagePath);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ImageCell_Click");
        }
    }

    private async Task UseImageAsync(string? imagePath)
    {
        var imageFolderPath = GetValidatedImageFolderPath(false);
        if (string.IsNullOrEmpty(_selectedRomFileName) || string.IsNullOrEmpty(imagePath) ||
            string.IsNullOrEmpty(imageFolderPath))
            return;

        var verification = await TryVerifyImageAsync(_selectedRomFileName, imagePath);
        if (verification is { IsMatch: false })
        {
            var choice = MessageBox.Show(
                $"AI thinks this image is not a cover for '{_selectedRomFileName}'.\n\n" +
                $"{verification.Reason}\n\nSave it anyway?",
                "AI Verification", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes) return;
        }

        var safeFileName = SearchQueryHelper.SanitizeFileName(_selectedRomFileName);
        var newFileName = Path.Combine(imageFolderPath, safeFileName + ".png");
        _imageFolderWatcher?.PreRegisterExpectedFile(newFileName);

        try
        {
            var result = await ImageProcessor.ConvertAndSaveImageAsync(imagePath, newFileName, CancellationToken.None);
            if (result.Success)
            {
                App.AudioService.PlayClickSound();
                RemoveSelectedItem();
                SimilarImages.Clear();
                UpdateMissingCount();
            }
            else
            {
                MessageBox.Show("Failed to save the image.", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error saving image: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            LogService.Error(ex, $"Unexpected error in UseImage: {imagePath}");
        }
    }

    private void Image_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { DataContext: ImageData imageData } element) return;

            if (imageData.ImagePath != null)
                element.ContextMenu = ButtonFactory.CreateContextMenu(imageData.ImagePath,
                    path => _ = UseImageAsync(path), element.ContextMenu);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in Image_ContextMenuOpening");
        }
    }
}