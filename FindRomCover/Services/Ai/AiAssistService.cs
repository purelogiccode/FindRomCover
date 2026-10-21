using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FindRomCover.Interfaces;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Coordinates AI-assisted cover selection: filters local candidates, prepares images,
///     calls the configured vision model, caches verdicts, and verifies covers on save.
/// </summary>
public sealed class AiAssistService : IDisposable
{
    private const int RemoteImageTimeoutSeconds = 15;

    private readonly SettingsManager _settings;
    private readonly IVisionModelClient? _client;
    private readonly bool _ownsClient;
    private readonly AiVerdictCache _cache;
    private readonly HttpClient _imageHttpClient;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiAssistService"/> class.
    /// </summary>
    /// <param name="settings">The settings manager providing the AI options.</param>
    /// <param name="client">An optional pre-built vision model client; when <see langword="null"/>, one is created per request and owned by this service.</param>
    /// <param name="cache">The verdict cache to use; defaults to the shared instance.</param>
    /// <param name="imageHttpClient">The HTTP client used for image downloads; defaults to the shared helper client.</param>
    public AiAssistService(
        SettingsManager settings,
        IVisionModelClient? client = null,
        AiVerdictCache? cache = null,
        HttpClient? imageHttpClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _client = client;
        _ownsClient = client == null;
        _cache = cache ?? AiVerdictCache.GetShared();
        _imageHttpClient = imageHttpClient ?? HttpClientHelper.Client;
    }

    /// <summary>
    ///     Gets a value indicating whether AI assistance is enabled and this instance has not been disposed.
    /// </summary>
    public bool IsEnabled => !_disposed && _settings.AiAssistEnabled;

    /// <summary>
    ///     Picks the best local candidate image for the given game using the vision model.
    /// </summary>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="candidates">The local candidate images to consider.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pick verdict, or <see langword="null"/> when AI assistance is disabled or no candidate qualifies.</returns>
    public async Task<AiPickResult?> PickBestAsync(
        string romName,
        string searchName,
        IReadOnlyList<ImageData> candidates,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || candidates.Count == 0) return null;

        var options = _settings.GetAiVisionOptions();
        var filtered = new List<ImageData>();
        var originalIndices = new List<int>();
        for (var i = 0; i < candidates.Count; i++)
            if (candidates[i].SimilarityScore >= options.CandidateThreshold)
            {
                filtered.Add(candidates[i]);
                originalIndices.Add(i);
            }

        if (filtered.Count == 0)
        {
            LogService.Debug(
                $"AI assist: no local candidates at or above the AI similarity threshold ({options.CandidateThreshold:0}%).");
            return null;
        }

        var inputs = await Task.Run(
                () => PrepareLocalCandidates(filtered, options.MaxCandidates, options.ImageMaxDimension),
                cancellationToken)
            .ConfigureAwait(false);
        if (inputs.Count == 0) return null;

        // SourceIndex from PrepareLocalCandidates points into the threshold-filtered
        // list; remap it so BestIndex always indexes the caller's original list.
        for (var i = 0; i < inputs.Count; i++)
            inputs[i] = inputs[i] with { SourceIndex = originalIndices[inputs[i].SourceIndex] };

        return await PickBestCoreAsync(romName, searchName, inputs, options, AiPickKind.Local, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Picks the best cover among remote API search results using the vision model.
    /// </summary>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="candidates">The remote candidate images to consider.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The pick verdict, or <see langword="null"/> when AI assistance is disabled or no image could be prepared.</returns>
    public async Task<AiPickResult?> PickBestForApiAsync(
        string romName,
        string searchName,
        IReadOnlyList<ImageData> candidates,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || candidates.Count == 0) return null;

        var options = _settings.GetAiVisionOptions();
        var inputs = await PrepareRemoteCandidatesAsync(
                _imageHttpClient,
                candidates,
                options.MaxCandidates,
                options.ImageMaxDimension,
                cancellationToken)
            .ConfigureAwait(false);

        if (inputs.Count == 0) return null;

        return await PickBestCoreAsync(romName, searchName, inputs, options, AiPickKind.ApiFallback, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Verifies that the image at the given path is cover art for the specified game.
    /// </summary>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="imagePath">The path of the image to verify.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The verification verdict, or <see langword="null"/> when verification is disabled or the image cannot be prepared.</returns>
    public async Task<AiVerificationResult?> VerifyAsync(
        string romName,
        string searchName,
        string imagePath,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || !_settings.AiVerifyOnSave) return null;

        var options = _settings.GetAiVisionOptions();

        PreparedVisionImage prepared;
        try
        {
            prepared = await Task.Run(
                    () => VisionImagePreparer.Prepare(imagePath, options.ImageMaxDimension),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, $"AI assist: could not prepare '{imagePath}' for verification.");
            return null;
        }

        var input = new VisionImageInput(Path.GetFileName(imagePath), prepared, 0);
        var cacheKey = BuildCacheKey("verify", options, romName, searchName, [input]);

        if (_cache.TryGet<AiVerificationResult>(cacheKey, out var cached) && cached != null)
        {
            LogService.Debug("AI assist: using cached verification verdict.");
            return cached;
        }

        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = ResolveClient(options);
            try
            {
                var result = await client
                    .VerifyAsync(options, romName, searchName, input, cancellationToken)
                    .ConfigureAwait(false);

                _cache.Set(cacheKey, result);
                LogService.Information(
                    $"AI assist: verification match={result.IsMatch} confidence={result.Confidence:P0}.");
                return result;
            }
            finally
            {
                if (_client == null) client.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "AI assist: verification request failed.");
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    /// <summary>
    ///     Disposes the vision model client when this service created it. The request
    ///     semaphore is intentionally left undisposed because in-flight requests may
    ///     still release it while the owner shuts down.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        if (_ownsClient) _client?.Dispose();
    }

    /// <summary>
    ///     Prepares up to <paramref name="maxCandidates"/> existing local images for a vision request.
    /// </summary>
    /// <param name="candidates">The candidate images to prepare.</param>
    /// <param name="maxCandidates">The maximum number of candidates to prepare.</param>
    /// <param name="maxDimension">The maximum image dimension in pixels.</param>
    /// <returns>The prepared image inputs whose source indexes refer to the supplied candidate list.</returns>
    internal static List<VisionImageInput> PrepareLocalCandidates(
        IReadOnlyList<ImageData> candidates,
        int maxCandidates,
        int maxDimension)
    {
        var inputs = new List<VisionImageInput>();
        var limit = Math.Min(Math.Max(1, maxCandidates), candidates.Count);

        for (var i = 0; i < limit; i++)
        {
            var path = candidates[i].ImagePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

            try
            {
                var prepared = VisionImagePreparer.Prepare(path, maxDimension);
                var name = string.IsNullOrWhiteSpace(candidates[i].ImageName)
                    ? Path.GetFileNameWithoutExtension(path)
                    : candidates[i].ImageName!;

                inputs.Add(new VisionImageInput(name, prepared, i));
            }
            catch (Exception ex)
            {
                LogService.WarningTransient(ex, $"AI assist: could not prepare '{path}' for analysis.");
            }
        }

        return inputs;
    }

    /// <summary>
    ///     Downloads and prepares remote candidate images, preferring thumbnails and falling back to full images.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to download the images.</param>
    /// <param name="candidates">The candidate images to prepare.</param>
    /// <param name="maxCandidates">The maximum number of candidates to prepare.</param>
    /// <param name="maxDimension">The maximum image dimension in pixels.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The prepared image inputs whose source indexes refer to the supplied candidate list.</returns>
    internal static async Task<List<VisionImageInput>> PrepareRemoteCandidatesAsync(
        HttpClient httpClient,
        IReadOnlyList<ImageData> candidates,
        int maxCandidates,
        int maxDimension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var inputs = new List<VisionImageInput>();
        var limit = Math.Min(Math.Max(1, maxCandidates), candidates.Count);

        for (var i = 0; i < limit; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preferredUrl = string.IsNullOrWhiteSpace(candidates[i].ThumbnailUrl)
                ? candidates[i].ImagePath
                : candidates[i].ThumbnailUrl;
            var fallbackUrl = string.Equals(preferredUrl, candidates[i].ImagePath,
                StringComparison.OrdinalIgnoreCase)
                ? null
                : candidates[i].ImagePath;

            PreparedVisionImage? prepared = null;
            var usedUrl = preferredUrl;
            foreach (var url in new[] { preferredUrl, fallbackUrl })
            {
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    prepared = await TryDownloadPreparedImageAsync(httpClient, url, maxDimension, cancellationToken)
                        .ConfigureAwait(false);
                    if (prepared != null)
                    {
                        usedUrl = url;
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogService.WarningTransient(ex, $"AI assist: could not prepare remote image '{url}'.");
                }
            }

            if (prepared == null) continue;

            var name = string.IsNullOrWhiteSpace(candidates[i].ImageName)
                ? Path.GetFileNameWithoutExtension(usedUrl) ?? usedUrl ?? "image"
                : candidates[i].ImageName!;

            inputs.Add(new VisionImageInput(name, prepared, i));
        }

        return inputs;
    }

    private static async Task<PreparedVisionImage?> TryDownloadPreparedImageAsync(
        HttpClient httpClient,
        string url,
        int maxDimension,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(RemoteImageTimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(ImageSaveService.BrowserUserAgent);
        request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");

        using var response = await httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            LogService.Debug($"AI assist: image download failed ({(int)response.StatusCode}) for '{url}'.");
            return null;
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is > ImageSaveService.MaxDownloadBytes)
        {
            LogService.Debug($"AI assist: image download exceeded the size limit for '{url}'.");
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
        var bytes = await ImageSaveService
            .ReadAllBytesBoundedAsync(stream, ImageSaveService.MaxDownloadBytes, timeoutCts.Token)
            .ConfigureAwait(false);
        if (bytes == null)
        {
            LogService.Debug($"AI assist: image download exceeded the size limit for '{url}'.");
            return null;
        }

        return VisionImagePreparer.Prepare(bytes, maxDimension);
    }

    private async Task<AiPickResult?> PickBestCoreAsync(
        string romName,
        string searchName,
        List<VisionImageInput> inputs,
        AiVisionOptions options,
        AiPickKind kind,
        CancellationToken cancellationToken)
    {
        var cacheKind = kind == AiPickKind.ApiFallback ? "pick-api" : "pick";
        var cacheKey = BuildCacheKey(cacheKind, options, romName, searchName, inputs);
        if (_cache.TryGet<AiPickResult>(cacheKey, out var cached) && cached != null)
        {
            LogService.Debug("AI assist: using cached pick verdict.");
            return cached;
        }

        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = ResolveClient(options);
            try
            {
                var result = await client
                    .PickBestAsync(options, romName, searchName, inputs, cancellationToken, kind)
                    .ConfigureAwait(false);

                var mapped = new AiPickResult
                {
                    BestIndex = result.BestIndex >= 0 && result.BestIndex < inputs.Count
                        ? inputs[result.BestIndex].SourceIndex
                        : -1,
                    Confidence = result.Confidence,
                    Reason = result.Reason
                };

                _cache.Set(cacheKey, mapped);
                LogService.Information(
                    $"AI assist: picked candidate {mapped.BestIndex} with confidence {mapped.Confidence:P0}.");
                return mapped;
            }
            finally
            {
                if (_client == null) client.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.WarningTransient(ex, "AI assist: pick request failed.");
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private IVisionModelClient ResolveClient(AiVisionOptions options)
    {
        return _client ?? VisionClientFactory.Create(options);
    }

    private static string BuildCacheKey(
        string kind,
        AiVisionOptions options,
        string romName,
        string searchName,
        IReadOnlyList<VisionImageInput> inputs)
    {
        // Length-prefix every free-form part so values containing the delimiter cannot
        // collide with a different (romName, searchName) pair.
        var builder = new StringBuilder();
        AppendKeyPart(builder, AppConstants.AiProviders.PromptVersion);
        AppendKeyPart(builder, kind);
        AppendKeyPart(builder, options.Provider);
        AppendKeyPart(builder, options.Model);
        AppendKeyPart(builder, romName);
        AppendKeyPart(builder, searchName);

        foreach (var input in inputs)
        {
            AppendKeyPart(builder, input.Image.Hash);
            AppendKeyPart(builder, input.SourceIndex.ToString(CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendKeyPart(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }

    private static string FriendlyMessage(Exception ex)
    {
        return ex is InvalidOperationException ? ex.Message : $"AI assist failed: {ex.Message}";
    }
}