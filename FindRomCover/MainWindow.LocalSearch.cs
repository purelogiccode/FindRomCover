using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

/// <summary>
///     Main application window. This part contains the local similarity search, the
///     AI-assisted pick of local candidates and the image saving logic.
/// </summary>
public partial class MainWindow
{
    private CancellationTokenSource? _webSearchCts;

    private void TriggerActiveTabSearch(string searchName)
    {
        var activeTab = SearchTabControl.SelectedIndex;

        // Build the search query for web/API tabs
        var extraQuery = TxtExtraQuery.Text?.Trim() ?? string.Empty;
        var cleanedSearchName = SearchQueryHelper.CleanSearchQuery(searchName);
        var searchQuery = !string.IsNullOrWhiteSpace(extraQuery)
            ? $"\"{cleanedSearchName}\" {extraQuery}"
            : $"\"{cleanedSearchName}\"";

        // Dispatch based on active tab
        switch (activeTab)
        {
            case 1: // Google Web
                SetGoogleWebSearching(true);
                StatusMessage.Text = "Searching Google...";
                _ = HandleGoogleWebSearchAsync(searchQuery);
                break;
            case 2: // Bing Web
                SetBingWebSearching(true);
                StatusMessage.Text = "Searching Bing...";
                _ = HandleBingWebSearchAsync(searchQuery);
                break;
            case 3: // Google API
                if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
                {
                    _ = ShowApiSettingsAndResumeAsync(searchQuery);
                    SetApiSearching(false);
                    break;
                }

                SetApiSearching(true);
                StatusMessage.Text = "Searching Google API...";
                _webSearchCts?.Cancel();
                _webSearchCts?.Dispose();
                _webSearchCts = new CancellationTokenSource();
                LblApiSearchQuery.Content = BuildQueryLabel("API search for: ", searchQuery);
                _ = HandleApiSearchAsync(searchQuery, _webSearchCts);
                break;
        }
    }

    private async Task ShowApiSettingsAndResumeAsync(string searchQuery)
    {
        await Dialogs.ShowMessageAsync(this,
            "A Google API key is required to use the Google API search.\n\nPlease enter your API key in the settings window that will open.",
            "API Key Required");

        if (_disposed) return;

        var apiSettingsWindow = new ApiSettingsWindow(Settings);
        await apiSettingsWindow.ShowDialog(this);

        if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
        {
            StatusMessage.Text = "API key not set. Google API search is unavailable.";
            return;
        }

        SetApiSearching(true);
        StatusMessage.Text = "Searching Google API...";
        _webSearchCts?.Cancel();
        _webSearchCts?.Dispose();
        _webSearchCts = new CancellationTokenSource();
        LblApiSearchQuery.Content = BuildQueryLabel("API search for: ", searchQuery);
        _ = HandleApiSearchAsync(searchQuery, _webSearchCts);
    }

    private static TextBlock BuildQueryLabel(string prefix, string query)
    {
        var textBlock = new TextBlock();
        textBlock.Inlines!.Add(new Run(prefix));
        textBlock.Inlines.Add(new Run(query) { FontWeight = Avalonia.Media.FontWeight.Bold });
        return textBlock;
    }

    private void ApplyDeterministicImageOrder(SimilarityCalculationResult similarityResult)
    {
        // Images are streamed into the collection as parallel loads finish, so the
        // visible order varies between runs. Re-apply the ranked order from the
        // calculator (score, then path) once all pending additions have been applied.
        for (var targetIndex = 0; targetIndex < similarityResult.SimilarImages.Count; targetIndex++)
        {
            var currentIndex = SimilarImages.IndexOf(similarityResult.SimilarImages[targetIndex]);
            if (currentIndex > targetIndex)
                SimilarImages.Move(currentIndex, targetIndex);
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

        var cts = new CancellationTokenSource();
        _findSimilarCts = cts;
        var cancellationToken = cts.Token;

        IsFindingSimilar = true;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested) return;

            var textBlock = new TextBlock();
            textBlock.Inlines!.Add(new Run("Search Query: "));
            textBlock.Inlines.Add(new Run($"{searchName} ") { FontWeight = Avalonia.Media.FontWeight.Bold });
            textBlock.Inlines.Add(new Run("for ROM: "));
            textBlock.Inlines.Add(new Run($"{romName} ") { FontWeight = Avalonia.Media.FontWeight.Bold });
            textBlock.Inlines.Add(new Run("with "));
            textBlock.Inlines.Add(new Run($"{Settings.SelectedSimilarityAlgorithm} ")
            { FontWeight = Avalonia.Media.FontWeight.Bold });
            textBlock.Inlines.Add(new Run("algorithm"));
            if (Settings.IgnoreBracketedText)
                textBlock.Inlines.Add(new Run(" (ignoring bracketed text)"));
            LblLocalSearchQuery.Content = textBlock;

            DisposeAndClearImages(SimilarImages);
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
                            _ = Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    SimilarImages.Add(imageData);
                                    HasSearchedSimilar = true;
                                }
                            });
                        },
                        Settings.IgnoreBracketedText
                    );
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                        await Dispatcher.UIThread.InvokeAsync(static () => { });
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    HasSearchedSimilar = true;

                    ApplyDeterministicImageOrder(similarityResult);

                    if (similarityResult.ProcessingErrors.Count > 0)
                    {
                        var errorSummary =
                            $"Encountered {similarityResult.ProcessingErrors.Count} issues while processing images:\n\n";
                        errorSummary += string.Join("\n", similarityResult.ProcessingErrors.Take(5));
                        if (similarityResult.ProcessingErrors.Count > 5)
                            errorSummary += $"\n...and {similarityResult.ProcessingErrors.Count - 5} more.";

                        Dialogs.ShowMessage(this, errorSummary, "Image Processing Warnings", MessageIcon.Warning);
                    }

                    LocalImageScrollViewer.ScrollToHome();

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
            if (ReferenceEquals(_findSimilarCts, cts))
                await Dispatcher.UIThread.InvokeAsync(() => DisposeAndClearImages(SimilarImages));
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Dialogs.ShowMessage(this, $"Error searching for similar images: {ex.Message}", "Error",
                    MessageIcon.Error);
                LogService.Error(ex, "Error in RunLocalSearchAsync");
            }
        }
        finally
        {
            // Only the current run may clear the flag — a stale run that was
            // superseded by a newer selection must not stop the new run's spinner.
            if (ReferenceEquals(_findSimilarCts, cts))
                IsFindingSimilar = false;
        }
    }

    private async Task RunAiPickAsync()
    {
        if (!Settings.AiAssistEnabled)
        {
            await Dialogs.ShowMessageAsync(this,
                "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                "AI Assist");
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

            if (!result.HasPick || result.BestIndex < 0 || result.BestIndex >= candidates.Count)
            {
                StatusMessage.Text = $"AI found no genuine cover among the candidates. {result.Reason}".Trim();
                return;
            }

            // Only record the query when the model actually found a pick. Recording
            // declined runs would make the batch skip this ROM for 180 days even
            // though nothing was ever found.
            MarkAiQuery(selectedItem);

            foreach (var image in candidates) image.AiBadge = string.Empty;

            var picked = candidates[result.BestIndex];
            picked.AiBadge = $"AI pick {result.Confidence:P0}";

            var currentIndex = SimilarImages.IndexOf(picked);
            if (currentIndex > 0) SimilarImages.Move(currentIndex, 0);

            LocalImageScrollViewer.ScrollToHome();
            StatusMessage.Text = $"AI picked '{picked.ImageName}' ({result.Confidence:P0}). {result.Reason}".Trim();

            if (Settings.AiAutoSave && result.Confidence * 100 >= Settings.AiAutoSaveThreshold &&
                picked.ImagePath != null)
                await UseImageAsync(selectedItem.RomName, picked.ImagePath);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI assist failed");
            StatusMessage.Text = "AI assist failed. Check the log for details.";
            Dialogs.ShowMessage(this, ex.Message, "AI Assist", MessageIcon.Warning);
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
            LogService.WarningTransient(ex, "AI assist: could not record the query in the history.");
        }
    }

    private async Task UseImageAsync(string romName, string? imagePath)
    {
        var imageFolderPath = GetValidatedImageFolderPath(false);
        if (string.IsNullOrEmpty(romName) || string.IsNullOrEmpty(imagePath) ||
            string.IsNullOrEmpty(imageFolderPath))
            return;

        var verification = await TryVerifyImageAsync(romName, imagePath);
        if (verification is { IsMatch: false })
        {
            var choice = await Dialogs.ShowQuestionAsync(this,
                $"AI thinks this image is not a cover for '{romName}'.\n\n" +
                $"{verification.Reason}\n\nSave it anyway?",
                "AI Verification");

            if (choice != MessageResult.Yes) return;
        }

        var safeFileName = SearchQueryHelper.SanitizeFileName(romName);
        var newFileName = Path.Combine(imageFolderPath, safeFileName + ".png");
        _imageFolderWatcher?.PreRegisterExpectedFile(newFileName);

        try
        {
            var result = await ImageProcessor.ConvertAndSaveImageAsync(imagePath, newFileName, CancellationToken.None);
            if (result.Success)
            {
                App.AudioService.PlayClickSound();
                RemoveMissingItemByName(romName);
                if (string.Equals(_selectedRomFileName, romName, StringComparison.OrdinalIgnoreCase))
                    DisposeAndClearImages(SimilarImages);
                UpdateMissingCount();
            }
            else
            {
                _imageFolderWatcher?.UnregisterExpectedFile(newFileName);
                var detail = result.ErrorMessage ?? "Failed to save the image.";
                LogService.Warning(
                    $"UseImage: could not save '{newFileName}' from '{imagePath}': {result.LogContext ?? detail}");
                Dialogs.ShowMessage(this, detail, "Save Failed", MessageIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _imageFolderWatcher?.UnregisterExpectedFile(newFileName);
            Dialogs.ShowMessage(this, $"Unexpected error saving image: {ex.Message}", "Error", MessageIcon.Error);
            LogService.Error(ex, $"Unexpected error in UseImage: {imagePath}");
        }
    }
}