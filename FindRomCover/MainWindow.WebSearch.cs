using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
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
            if (!await EnsureWebViewReadyAsync(BingWebView))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    IsSearching = false;
                    StatusMessage.Text = "Web view component is not ready.";
                });
                return;
            }

            var bingUrl = WebSearchService.BuildBingSearchUrl(searchQuery);

            await Dispatcher.InvokeAsync(() =>
            {
                LblBingWebQuery.Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run("Web search for: ") { FontWeight = FontWeights.Normal },
                        new Run(searchQuery) { FontWeight = FontWeights.Bold },
                        new Run(" (Bing)")
                    }
                };
                StatusMessage.Text = "Loading Bing web search...";
                BingWebView.CoreWebView2.Navigate(bingUrl);
            });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in HandleBingWebSearchAsync");
            await Dispatcher.InvokeAsync(() =>
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
            if (!await EnsureWebViewReadyAsync(GoogleWebView))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    IsSearching = false;
                    StatusMessage.Text = "Web view component is not ready.";
                });
                return;
            }

            var googleUrl = WebSearchService.BuildGoogleSearchUrl(searchQuery);

            await Dispatcher.InvokeAsync(() =>
            {
                LblGoogleWebQuery.Content = new TextBlock
                {
                    Inlines =
                    {
                        new Run("Web search for: ") { FontWeight = FontWeights.Normal },
                        new Run(searchQuery) { FontWeight = FontWeights.Bold },
                        new Run(" (Google)")
                    }
                };
                StatusMessage.Text = "Loading Google web search...";
                GoogleWebView.CoreWebView2.Navigate(googleUrl);
            });
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in HandleGoogleWebSearchAsync");
            await Dispatcher.InvokeAsync(() =>
            {
                IsSearching = false;
                StatusMessage.Text = "Error loading Google search.";
            });
        }
    }

    private async Task HandleApiSearchAsync(string searchQuery, CancellationToken token)
    {
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
                await Dispatcher.InvokeAsync(static () =>
                {
                    MessageBox.Show("Please configure your API keys in Settings > API Settings.", "Missing API Key",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                coverImageUrls = [];
            }
            catch (InvalidOperationException ex)
            {
                // Google API errors (invalid API key, rate limits, network failures) are
                // configuration/environment issues, not application bugs. Inform the user
                // directly instead of reporting an error to the bug API.
                LogService.Warning(ex, "Google API search failed");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(ex.Message, "Google API Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    IsSearching = false;
                    StatusMessage.Text = "API search failed.";
                });
                coverImageUrls = [];
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => StatusMessage.Text = "Search canceled.");
                return;
            }

            if (token.IsCancellationRequested) return;

            var thumbnailSize = Settings.ThumbnailSize;

            await Dispatcher.InvokeAsync(() =>
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
            await Dispatcher.InvokeAsync(() =>
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

    private async void SaveApiImage_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { DataContext: ImageData { ImagePath: not null } imageData }) return;

            await SaveApiImageAsync(imageData);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error saving API image");
        }
    }

    private async Task SaveApiImageAsync(ImageData imageData)
    {
        var imageFolderPath = GetValidatedImageFolderPath(false);
        if (string.IsNullOrEmpty(_selectedRomFileName) || string.IsNullOrEmpty(imageFolderPath) ||
            string.IsNullOrEmpty(imageData.ImagePath))
            return;

        try
        {
            var safeFileName = SearchQueryHelper.SanitizeFileName(_selectedRomFileName);
            var newFileName = Path.Combine(imageFolderPath, safeFileName + ".png");
            _imageFolderWatcher?.PreRegisterExpectedFile(newFileName);
            var result = await ImageSaveService.DownloadAndSaveImageAsync(imageData.ImagePath,
                imageData.ThumbnailUrl, newFileName, Settings.AiMinCoverWidth);

            if (!result)
            {
                MessageBox.Show(
                    "The image could not be downloaded. The server may have blocked the request or the image is no longer available.\n\nPlease try a different image.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var verification = await TryVerifyImageAsync(_selectedRomFileName, newFileName);
            if (verification is { IsMatch: false })
            {
                var choice = MessageBox.Show(
                    $"AI thinks this image is not a cover for '{_selectedRomFileName}'.\n\n" +
                    $"{verification.Reason}\n\nKeep the downloaded file anyway?",
                    "AI Verification", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (choice != MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(newFileName)) File.Delete(newFileName);
                    }
                    catch (Exception deleteEx)
                    {
                        LogService.Warning(deleteEx, $"Could not remove unverified image '{newFileName}'.");
                    }

                    return;
                }
            }

            App.AudioService.PlayClickSound();
            RemoveMissingItemByName(_selectedRomFileName);
            PanelImages.Clear();
            UpdateMissingCount();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving image: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            LogService.Error(ex, "Error saving API image");
        }
    }

    /// <summary>
    ///     Downloads the remote thumbnail with the app's HttpClient (browser user-agent,
    ///     bounded timeout) and shows it via <see cref="ImageData.ImageSource"/>. WPF never
    ///     touches the raw remote URL directly, so hosts that block no-User-Agent requests
    ///     still display thumbnails and the UI thread is not blocked by network I/O.
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

                var bitmap = await Task.Run(() => DecodeThumbnail(bytes));
                if (bitmap == null) return;

                await Application.Current.Dispatcher.InvokeAsync(() => { imageData.ImageSource = bitmap; });
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

    private static BitmapImage? DecodeThumbnail(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();

            if (bitmap.CanFreeze) bitmap.Freeze();

            return bitmap;
        }
        catch (Exception ex)
        {
            LogService.Debug($"API thumbnail decode failed: {ex.Message}");
            return null;
        }
    }

    private async void BtnAiPickApi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunAiPickApiAsync();
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Error in BtnAiPickApi_Click");
        }
    }

    private async Task RunAiPickApiAsync()
    {
        if (!Settings.AiAssistEnabled)
        {
            MessageBox.Show(
                "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                "AI Assist", MessageBoxButton.OK, MessageBoxImage.Information);
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

            ApiImageScrollViewer.ScrollToTop();
            StatusMessage.Text = $"AI picked '{picked.ImageName}' ({result.Confidence:P0}). {result.Reason}".Trim();

            if (Settings.AiAutoSave && result.Confidence * 100 >= Settings.AiAutoSaveThreshold)
                await SaveApiImageAsync(picked);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI assist failed for API results");
            StatusMessage.Text = "AI assist failed. Check the log for details.";
            MessageBox.Show(ex.Message, "AI Assist", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsAiBusy = false;
        }
    }
}