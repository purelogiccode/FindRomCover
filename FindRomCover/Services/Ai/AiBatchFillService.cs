using System.IO;
using FindRomCover.ApiProvider;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class AiBatchFillService
{
    private readonly SettingsManager _settings;
    private readonly AiAssistService _aiAssist;
    private readonly AiQueryHistory _queryHistory;
    private readonly Action<string>? _preRegisterExpectedFile;

    public AiBatchFillService(
        SettingsManager settings,
        AiAssistService aiAssist,
        Action<string>? preRegisterExpectedFile = null,
        AiQueryHistory? queryHistory = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _aiAssist = aiAssist ?? throw new ArgumentNullException(nameof(aiAssist));
        _preRegisterExpectedFile = preRegisterExpectedFile;
        _queryHistory = queryHistory ?? new AiQueryHistory();
    }

    public async Task<List<AiBatchItemResult>> RunAsync(
        IReadOnlyList<MissingImageItem> items,
        string imageFolderPath,
        bool useApiFallback,
        string? extraQuery,
        IProgress<AiBatchItemResult>? progress,
        CancellationToken cancellationToken,
        bool skipPreviouslyQueried = true)
    {
        var results = new List<AiBatchItemResult>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AiBatchItemResult result;
            try
            {
                result = await ProcessItemAsync(item, imageFolderPath, useApiFallback, extraQuery,
                        skipPreviouslyQueried, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, $"AI batch fill: item '{item.RomName}' failed.");
                result = new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed, ex.Message);
            }

            results.Add(result);
            progress?.Report(result);
        }

        return results;
    }

    private async Task<AiBatchItemResult> ProcessItemAsync(
        MissingImageItem item,
        string imageFolderPath,
        bool useApiFallback,
        string? extraQuery,
        bool skipPreviouslyQueried,
        CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(imageFolderPath, SearchQueryHelper.SanitizeFileName(item.RomName) + ".png");
        var existingCoverPath = CoverFileResolver.FindCover(imageFolderPath, item.RomName);
        if (existingCoverPath != null)
            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedAlreadyExists,
                "Cover already exists.", existingCoverPath);

        if (skipPreviouslyQueried && _queryHistory.WasQueried(targetPath))
            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedAlreadyQueried,
                "Already queried in a previous session.", targetPath);

        var localCandidates = await SimilarityCalculator.FindTopCandidatesAsync(
                item.SearchName,
                imageFolderPath,
                _settings.AiCandidateThreshold,
                _settings.SelectedSimilarityAlgorithm,
                cancellationToken,
                Math.Max(1, _settings.AiMaxCandidates))
            .ConfigureAwait(false);

        AiPickResult? pick = null;
        if (localCandidates.Count > 0)
        {
            var images = localCandidates
                .Select(static candidate => new ImageData(candidate.FilePath, candidate.ImageName, candidate.SimilarityScore))
                .ToList();

            pick = await _aiAssist.PickBestAsync(item.RomName, item.SearchName, images, cancellationToken)
                .ConfigureAwait(false);

            if (IsUsablePick(pick, images.Count) && images[pick!.BestIndex].ImagePath is { } localPath)
            {
                _preRegisterExpectedFile?.Invoke(targetPath);
                var saveResult = await ImageProcessor.ConvertAndSaveImageAsync(localPath, targetPath, cancellationToken)
                    .ConfigureAwait(false);

                if (saveResult.Success)
                {
                    _queryHistory.Remove(targetPath);
                    return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.FilledFromLocal,
                        $"AI pick '{images[pick.BestIndex].ImageName}' ({pick.Confidence:P0}).", targetPath);
                }

                return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed,
                    saveResult.ErrorMessage ?? "Failed to save image.");
            }
        }

        if (!useApiFallback || string.IsNullOrWhiteSpace(_settings.GoogleKey))
        {
            var outcome = localCandidates.Count == 0 ? AiBatchOutcome.NoCandidates : AiBatchOutcome.SkippedLowConfidence;
            if (outcome == AiBatchOutcome.SkippedLowConfidence && pick != null)
                _queryHistory.MarkQueried(targetPath, "local-no-match");

            return new AiBatchItemResult(item.RomName, item.SearchName, outcome,
                localCandidates.Count == 0
                    ? "No local candidates found."
                    : "AI found no confident local match.", targetPath);
        }

        List<ImageData> apiResults;
        try
        {
            apiResults = await Google
                .FetchImagesFromGoogleAsync(BuildApiQuery(item.SearchName, extraQuery), _settings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"AI batch fill: Google API search failed for '{item.RomName}'.");
            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed, ex.Message);
        }

        if (apiResults.Count == 0)
            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.NoCandidates,
                "No Google API results found.");

        var apiPick = await _aiAssist.PickBestForApiAsync(item.RomName, item.SearchName, apiResults, cancellationToken)
            .ConfigureAwait(false);

        var pickedApiImage = IsUsablePick(apiPick, apiResults.Count) ? apiResults[apiPick!.BestIndex] : null;
        if (pickedApiImage != null && IsConfident(apiPick) && pickedApiImage.ImagePath is { } imageUrl)
        {
            _preRegisterExpectedFile?.Invoke(targetPath);
            var saved = await ImageSaveService
                .DownloadAndSaveImageAsync(imageUrl, pickedApiImage.ThumbnailUrl, targetPath,
                    _settings.AiMinCoverWidth, cancellationToken)
                .ConfigureAwait(false);

            if (saved)
            {
                if (!await IsVerifiedAsync(item.RomName, targetPath, cancellationToken).ConfigureAwait(false))
                {
                    TryDelete(targetPath);
                    _queryHistory.MarkQueried(targetPath, "api-verification-rejected");
                    return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedLowConfidence,
                        "AI verification rejected the downloaded image.", targetPath);
                }

                _queryHistory.Remove(targetPath);
                if (apiPick != null)
                    return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.FilledFromApi,
                        $"AI pick '{pickedApiImage.ImageName}' ({apiPick.Confidence:P0}).", targetPath);
            }

            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed,
                "The image could not be downloaded.");
        }

        if (apiPick != null)
            _queryHistory.MarkQueried(targetPath, "api-no-match");

        return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedLowConfidence,
            "AI found no confident match.", targetPath);
    }

    private async Task<bool> IsVerifiedAsync(string romName, string imagePath, CancellationToken cancellationToken)
    {
        if (!_settings.AiAssistEnabled || !_settings.AiVerifyOnSave) return true;

        try
        {
            var result = await _aiAssist.VerifyAsync(romName, romName, imagePath, cancellationToken)
                .ConfigureAwait(false);
            return result is null || result.IsMatch;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI batch fill: verification failed; proceeding without verification.");
            return true;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"AI batch fill: could not remove unverified image '{path}'.");
        }
    }

    private bool IsConfident(AiPickResult? pick)
    {
        return pick is { HasPick: true } && pick.Confidence * 100 >= _settings.AiAutoSaveThreshold;
    }

    internal bool IsUsablePick(AiPickResult? pick, int candidateCount)
    {
        return pick != null && pick.BestIndex >= 0 && pick.BestIndex < candidateCount && IsConfident(pick);
    }

    private static string BuildApiQuery(string searchName, string? extraQuery)
    {
        var cleaned = SearchQueryHelper.CleanSearchQuery(searchName);
        var query = $"\"{cleaned}\"";
        if (!string.IsNullOrWhiteSpace(extraQuery)) query += $" {extraQuery.Trim()}";

        return query;
    }
}
