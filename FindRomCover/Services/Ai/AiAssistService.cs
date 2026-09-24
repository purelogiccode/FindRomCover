using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class AiAssistService : IDisposable
{
    private const int RemoteImageTimeoutSeconds = 15;

    private readonly SettingsManager _settings;
    private readonly IVisionModelClient? _client;
    private readonly AiVerdictCache _cache;
    private readonly HttpClient _imageHttpClient;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private bool _disposed;

    public AiAssistService(
        SettingsManager settings,
        IVisionModelClient? client = null,
        AiVerdictCache? cache = null,
        HttpClient? imageHttpClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _client = client;
        _cache = cache ?? AiVerdictCache.GetShared();
        _imageHttpClient = imageHttpClient ?? HttpClientHelper.Client;
    }

    public bool IsEnabled => !_disposed && _settings.AiAssistEnabled;

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
            LogService.Warning(ex, $"AI assist: could not prepare '{imagePath}' for verification.");
            return null;
        }

        var input = new VisionImageInput(Path.GetFileName(imagePath) ?? imagePath, prepared, 0);
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
            LogService.Warning(ex, "AI assist: verification request failed.");
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _requestLock.Dispose();
        _client?.Dispose();
    }

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
                    ? Path.GetFileNameWithoutExtension(path) ?? path
                    : candidates[i].ImageName!;

                inputs.Add(new VisionImageInput(name, prepared, i));
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, $"AI assist: could not prepare '{path}' for analysis.");
            }
        }

        return inputs;
    }

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
            foreach (var url in new[] { preferredUrl, fallbackUrl })
            {
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    prepared = await TryDownloadPreparedImageAsync(httpClient, url, maxDimension, cancellationToken)
                        .ConfigureAwait(false);
                    if (prepared != null) break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogService.Warning(ex, $"AI assist: could not prepare remote image '{url}'.");
                }
            }

            if (prepared == null) continue;

            var name = string.IsNullOrWhiteSpace(candidates[i].ImageName)
                ? Path.GetFileNameWithoutExtension(preferredUrl) ?? preferredUrl ?? "image"
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

        var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
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
            LogService.Warning(ex, "AI assist: pick request failed.");
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
        var raw = string.Join(
            '|',
            AppConstants.AiProviders.PromptVersion,
            kind,
            options.Provider,
            options.Model,
            romName,
            searchName,
            string.Join(',', inputs.Select(static i => i.Image.Hash)),
            string.Join(',', inputs.Select(static i => i.SourceIndex)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string FriendlyMessage(Exception ex)
    {
        return ex is InvalidOperationException ? ex.Message : $"AI assist failed: {ex.Message}";
    }
}
