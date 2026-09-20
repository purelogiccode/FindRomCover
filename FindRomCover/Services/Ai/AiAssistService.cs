using System.IO;
using System.Security.Cryptography;
using System.Text;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class AiAssistService : IDisposable
{
    private readonly SettingsManager _settings;
    private readonly OpenAiCompatibleVisionClient _client;
    private readonly AiVerdictCache _cache;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private bool _disposed;

    public AiAssistService(
        SettingsManager settings,
        OpenAiCompatibleVisionClient? client = null,
        AiVerdictCache? cache = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _client = client ?? new OpenAiCompatibleVisionClient();
        _cache = cache ?? new AiVerdictCache();
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
        var inputs = PrepareCandidates(candidates, options.MaxCandidates, options.ImageMaxDimension);
        if (inputs.Count == 0) return null;

        var cacheKey = BuildCacheKey("pick", options, romName, searchName, inputs);
        if (_cache.TryGet<AiPickResult>(cacheKey, out var cached) && cached != null)
        {
            LogService.Debug("AI assist: using cached pick verdict.");
            return cached;
        }

        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _client
                .PickBestAsync(options, romName, searchName, inputs, cancellationToken)
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
            prepared = VisionImagePreparer.Prepare(imagePath, options.ImageMaxDimension);
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
            var result = await _client
                .VerifyAsync(options, romName, searchName, input, cancellationToken)
                .ConfigureAwait(false);

            _cache.Set(cacheKey, result);
            LogService.Information(
                $"AI assist: verification match={result.IsMatch} confidence={result.Confidence:P0}.");
            return result;
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
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<VisionImageInput> PrepareCandidates(
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
            string.Join(',', inputs.Select(static i => i.Image.Hash)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string FriendlyMessage(Exception ex)
    {
        return ex is InvalidOperationException ? ex.Message : $"AI assist failed: {ex.Message}";
    }
}
