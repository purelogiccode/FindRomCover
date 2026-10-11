using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FindRomCover.ApiProvider;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

public partial class MainWindow
{
    private async Task HandleBingWebSearchAsync(string searchQuery)
    {
        try
        {
            if (!await EnsureWebViewReadyAsync(BingWebView, "Bing"))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsSearching = false;
                    StatusMessage.Text = "Web view component is not ready.";
                });
                return;
            }

            var bingUrl = WebSearchService.BuildBingSearchUrl(searchQuery);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LblBingWebQuery.Content = BuildWebQueryLabel(searchQuery, "Bing");
                StatusMessage.Text = "Loading Bing web search...";
                NavigateWebView(BingWebView, "Bing", bingUrl);
            });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in HandleBingWebSearchAsync");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearching = false;
                StatusMessage.Text = "Error loading Bing search.";
            });
        }
    }

    private async Task HandleGoogleWebSearchAsync(string searchQuery)
    {
        try
        {
            if (!await EnsureWebViewReadyAsync(GoogleWebView, "Google"))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsSearching = false;
                    StatusMessage.Text = "Web view component is not ready.";
                });
                return;
            }

            var googleUrl = WebSearchService.BuildGoogleSearchUrl(searchQuery);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LblGoogleWebQuery.Content = BuildWebQueryLabel(searchQuery, "Google");
                StatusMessage.Text = "Loading Google web search...";
                NavigateWebView(GoogleWebView, "Google", googleUrl);
            });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in HandleGoogleWebSearchAsync");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearching = false;
                StatusMessage.Text = "Error loading Google search.";
            });
        }
    }

    private static TextBlock BuildWebQueryLabel(string searchQuery, string engine)
    {
        var textBlock = new TextBlock();
        textBlock.Inlines!.Add(new Run("Web search for: "));
        textBlock.Inlines.Add(new Run(searchQuery) { FontWeight = Avalonia.Media.FontWeight.Bold });
        textBlock.Inlines.Add(new Run($" ({engine})"));
        return textBlock;
    }

    private async Task HandleApiSearchAsync(string searchQuery, CancellationTokenSource searchCts)
    {
        var token = searchCts.Token;

        try
        {
            // searchQuery is already built by TriggerActiveTabSearch as
            // "\"cleaned name\" extra terms" — pass it through unchanged. Re-quoting
            // the whole string would fold the extra terms into the exact phrase
            // (mirrors AiBatchFillService.BuildApiQuery).
            List<ImageData> coverImageUrls;
            try
            {
                coverImageUrls = await FetchImagesWithRetryAsync(searchQuery, token);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("API Key is not set",
                                                           StringComparison.OrdinalIgnoreCase))
            {
                await Dispatcher.UIThread.InvokeAsync(static () =>
                {
                    Dialogs.ShowMessage(null, "Please configure your API keys in Settings > API Settings.",
                        "Missing API Key", MessageIcon.Warning);
                });
                coverImageUrls = [];
            }
            catch (InvalidOperationException ex)
            {
                // Google API errors (invalid API key, rate limits, network failures) are
                // configuration/environment issues, not application bugs. Inform the user
                // directly instead of reporting an error to the bug API.
                LogService.Warning(ex, "Google API search failed");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Dialogs.ShowMessage(this, ex.Message, "Google API Error", MessageIcon.Warning);
                    IsSearching = false;
                    StatusMessage.Text = "API search failed.";
                });
                coverImageUrls = [];
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // A newer search (or a selection change) may have replaced this one;
                    // only the current search may clear the progress state.
                    if (!ReferenceEquals(_webSearchCts, searchCts)) return;

                    IsSearching = false;
                    StatusMessage.Text = "Search canceled.";
                });
                return;
            }

            if (token.IsCancellationRequested) return;

            var thumbnailSize = Settings.ThumbnailSize;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PanelImages.Clear();

                if (coverImageUrls.Count > 0)
                    foreach (var result in coverImageUrls)
                    {
                        result.ThumbnailWidth = thumbnailSize;
                        result.ThumbnailHeight = thumbnailSize;
                        PanelImages.Add(result);
                        LoadApiThumbnailAsync(result);
                    }

                IsSearching = false;
                HasSearchedApi = true;
                StatusMessage.Text = coverImageUrls.Count > 0
                    ? $"Found {coverImageUrls.Count} images."
                    : "No images found.";
                StatusImageCount.Text = PanelImages.Count.ToString(CultureInfo.InvariantCulture);
            });

            if (Settings.AiAutoRun && Settings.AiAssistEnabled && PanelImages.Count > 0)
                _ = RunAiPickApiAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in HandleApiSearchAsync");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearching = false;
                StatusMessage.Text = "Error during API search.";
            });
        }
    }

    private Task<List<ImageData>> FetchImagesWithRetryAsync(string searchQuery, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
            throw new InvalidOperationException("API Key is not set.");

        return Google.FetchImagesFromGoogleAsync(searchQuery, Settings, token);
    }

    private async Task SaveApiImageAsync(string romName, ImageData imageData)
    {
        var imageFolderPath = GetValidatedImageFolderPath(false);
        if (string.IsNullOrEmpty(romName) || string.IsNullOrEmpty(imageFolderPath) ||
            string.IsNullOrEmpty(imageData.ImagePath))
            return;

        var newFileName = Path.Combine(imageFolderPath, SearchQueryHelper.SanitizeFileName(romName) + ".png");
        // The ".tmp<hex>" suffix keeps the watcher away and lets startup cleanup
        // remove the file if the app crashes mid-download.
        var tempFileName = newFileName + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        var saved = false;

        try
        {
            var result = await ImageSaveService.DownloadAndSaveImageAsync(imageData.ImagePath,
                imageData.ThumbnailUrl, tempFileName, Settings.AiMinCoverWidth);

            if (!result)
            {
                await Dialogs.ShowMessageAsync(this,
                    "The image could not be downloaded. The server may have blocked the request or the image is no longer available.\n\nPlease try a different image.",
                    "Download Failed",
                    MessageIcon.Warning);
                return;
            }

            var verification = await TryVerifyImageAsync(romName, tempFileName);
            if (verification is { IsMatch: false })
            {
                var choice = await Dialogs.ShowQuestionAsync(this,
                    $"AI thinks this image is not a cover for '{romName}'.\n\n" +
                    $"{verification.Reason}\n\nKeep the downloaded file anyway?",
                    "AI Verification", MessageIcon.Warning);

                if (choice != MessageResult.Yes) return;
            }

            if (File.Exists(newFileName))
            {
                var replace = await Dialogs.ShowQuestionAsync(this,
                    $"A cover named '{Path.GetFileName(newFileName)}' already exists.\n\nReplace it?",
                    "Cover Exists", MessageIcon.Question);
                if (replace != MessageResult.Yes) return;
            }

            _imageFolderWatcher?.PreRegisterExpectedFile(newFileName);
            saved = await MoveFileWithRetryAsync(tempFileName, newFileName);
            if (!saved)
            {
                _imageFolderWatcher?.UnregisterExpectedFile(newFileName);
                await Dialogs.ShowMessageAsync(this, $"The image could not be saved to '{newFileName}'.", "Save Failed",
                    MessageIcon.Error);
                return;
            }
        }
        catch (Exception ex)
        {
            _imageFolderWatcher?.UnregisterExpectedFile(newFileName);
            await Dialogs.ShowMessageAsync(this, $"Error saving image: {ex.Message}", "Error", MessageIcon.Error);
            LogService.Error(ex, "Error saving API image");
            return;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFileName)) File.Delete(tempFileName);
            }
            catch (Exception cleanupEx)
            {
                LogService.Warning(cleanupEx, $"Could not remove temporary download '{tempFileName}'.");
            }
        }

        if (!saved) return;

        App.AudioService.PlayClickSound();
        RemoveMissingItemByName(romName);
        PanelImages.Clear();
        UpdateMissingCount();
    }

    private static async Task<bool> MoveFileWithRetryAsync(string sourcePath, string targetPath)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
            try
            {
                File.Move(sourcePath, targetPath, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= maxAttempts)
                {
                    LogService.Warning(ex, $"Could not move '{sourcePath}' to '{targetPath}'.");
                    return false;
                }

                await Task.Delay(150 * attempt);
            }

        return false;
    }

    /// <summary>
    ///     Downloads the remote thumbnail with the app's HttpClient (browser user-agent,
    ///     bounded timeout) and shows it via <see cref="ImageData.ImageSource"/>. The UI
    ///     never touches the raw remote URL directly, so hosts that block no-User-Agent
    ///     requests still display thumbnails and the UI thread is not blocked by network I/O.
    /// </summary>
    private static void LoadApiThumbnailAsync(ImageData imageData)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(imageData.ThumbnailUrl)
                    ? imageData.ImagePath
                    : imageData.ThumbnailUrl;
                if (string.IsNullOrWhiteSpace(url)) return;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd(ImageSaveService.BrowserUserAgent);

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var response = await HttpClientHelper.Client.SendAsync(request, timeoutCts.Token);
                if (!response.IsSuccessStatusCode) return;

                var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
                if (bytes.Length == 0) return;

                var bitmap = DecodeThumbnail(bytes);
                if (bitmap == null) return;

                await Dispatcher.UIThread.InvokeAsync(() => { imageData.ImageSource = bitmap; });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogService.Debug($"API thumbnail load failed for '{imageData.ImagePath}': {ex.Message}");
            }
        });
    }

    private static Bitmap? DecodeThumbnail(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            LogService.Debug($"API thumbnail decode failed: {ex.Message}");
            return null;
        }
    }

    private async Task RunAiPickApiAsync()
    {
        if (!Settings.AiAssistEnabled)
        {
            await Dialogs.ShowMessageAsync(this,
                "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                "AI Assist", MessageIcon.Information);
            return;
        }

        if (LstMissingImages.SelectedItem is not MissingImageItem selectedItem) return;
        if (PanelImages.Count == 0 || IsAiBusy) return;

        _aiAssistCts?.Cancel();
        _aiAssistCts?.Dispose();
        _aiAssistCts = new CancellationTokenSource();
        var cancellationToken = _aiAssistCts.Token;

        _aiAssistService ??= new AiAssistService(Settings);

        IsAiBusy = true;
        var candidates = PanelImages.ToList();
        StatusMessage.Text = $"AI is analyzing {candidates.Count} API result(s)...";

        try
        {
            var result = await _aiAssistService.PickBestForApiAsync(
                selectedItem.RomName,
                selectedItem.SearchName,
                candidates,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (result is null)
            {
                StatusMessage.Text = "AI did not analyze any API result.";
                return;
            }

            if (!result.HasPick || result.BestIndex < 0 || result.BestIndex >= candidates.Count)
            {
                StatusMessage.Text = $"AI found no genuine cover among the API results. {result.Reason}".Trim();
                return;
            }

            // Only record the query when the model actually found a pick. Recording
            // declined runs would make the batch skip this ROM for 180 days even
            // though nothing was ever found.
            MarkAiQuery(selectedItem);

            foreach (var image in candidates) image.AiBadge = string.Empty;

            var picked = candidates[result.BestIndex];
            picked.AiBadge = $"AI pick {result.Confidence:P0}";

            var currentIndex = PanelImages.IndexOf(picked);
            if (currentIndex > 0) PanelImages.Move(currentIndex, 0);

            ApiImageScrollViewer.ScrollToHome();
            StatusMessage.Text = $"AI picked '{picked.ImageName}' ({result.Confidence:P0}). {result.Reason}".Trim();

            if (Settings.AiAutoSave && result.Confidence * 100 >= Settings.AiAutoSaveThreshold)
                await SaveApiImageAsync(selectedItem.RomName, picked);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI assist failed for API results");
            StatusMessage.Text = "AI assist failed. Check the log for details.";
            await Dialogs.ShowMessageAsync(this, ex.Message, "AI Assist", MessageIcon.Warning);
        }
        finally
        {
            IsAiBusy = false;
        }
    }
}
