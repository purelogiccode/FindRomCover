using System.IO;
using FindRomCover.ApiProvider;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class AiBatchFillService
{
    private readonly SettingsManager _settings;
    private readonly AiAssistService _aiAssist;
    private readonly Action<string>? _preRegisterExpectedFile;

    public AiBatchFillService(
        SettingsManager settings,
        AiAssistService aiAssist,
        Action<string>? preRegisterExpectedFile = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _aiAssist = aiAssist ?? throw new ArgumentNullException(nameof(aiAssist));
        _preRegisterExpectedFile = preRegisterExpectedFile;
    }

    public async Task<List<AiBatchItemResult>> RunAsync(
        IReadOnlyList<MissingImageItem> items,
        string imageFolderPath,
        bool useApiFallback,
        string? extraQuery,
        IProgress<AiBatchItemResult>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<AiBatchItemResult>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AiBatchItemResult result;
            try
            {
                result = await ProcessItemAsync(item, imageFolderPath, useApiFallback, extraQuery, cancellationToken)
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
        CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(imageFolderPath, SearchQueryHelper.SanitizeFileName(item.RomName) + ".png");
        if (File.Exists(targetPath))
            return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedAlreadyExists,
                "Cover already exists.", targetPath);

        var localCandidates = await SimilarityCalculator.FindTopCandidatesAsync(
                item.SearchName,
                imageFolderPath,
                _settings.SimilarityThreshold,
                _settings.SelectedSimilarityAlgorithm,
                cancellationToken,
                Math.Max(1, _settings.AiMaxCandidates))
            .ConfigureAwait(false);

        if (localCandidates.Count > 0)
        {
            var images = localCandidates
                .Select(static candidate => new ImageData(candidate.FilePath, candidate.ImageName, candidate.SimilarityScore))
                .ToList();

            var pick = await _aiAssist.PickBestAsync(item.RomName, item.SearchName, images, cancellationToken)
                .ConfigureAwait(false);

            if (IsConfident(pick) && images[pick!.BestIndex].ImagePath is { } localPath)
            {
                _preRegisterExpectedFile?.Invoke(targetPath);
                var saveResult = await ImageProcessor.ConvertAndSaveImageAsync(localPath, targetPath, cancellationToken)
                    .ConfigureAwait(false);

                return saveResult.Success
                    ? new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.FilledFromLocal,
                        $"AI pick '{images[pick.BestIndex].ImageName}' ({pick.Confidence:P0}).", targetPath)
                    : new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed,
                        saveResult.ErrorMessage ?? "Failed to save image.");
            }
        }

        if (!useApiFallback || string.IsNullOrWhiteSpace(_settings.GoogleKey))
            return new AiBatchItemResult(item.RomName, item.SearchName,
                localCandidates.Count == 0 ? AiBatchOutcome.NoCandidates : AiBatchOutcome.SkippedLowConfidence,
                localCandidates.Count == 0
                    ? "No local candidates found."
                    : "AI found no confident local match.");

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

        if (IsConfident(apiPick) && apiResults[apiPick!.BestIndex].ImagePath is { } imageUrl)
        {
            _preRegisterExpectedFile?.Invoke(targetPath);
            var saved = await ImageSaveService.DownloadAndSaveImageAsync(imageUrl, targetPath, cancellationToken)
                .ConfigureAwait(false);

            return saved
                ? new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.FilledFromApi,
                    $"AI pick '{apiResults[apiPick.BestIndex].ImageName}' ({apiPick.Confidence:P0}).", targetPath)
                : new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.Failed,
                    "The image could not be downloaded.");
        }

        return new AiBatchItemResult(item.RomName, item.SearchName, AiBatchOutcome.SkippedLowConfidence,
            "AI found no confident match.");
    }

    private bool IsConfident(AiPickResult? pick)
    {
        return pick is { HasPick: true } && pick.Confidence * 100 >= _settings.AiAutoSaveThreshold;
    }

    private static string BuildApiQuery(string searchName, string? extraQuery)
    {
        var cleaned = SearchQueryHelper.CleanSearchQuery(searchName);
        var query = $"\"{cleaned}\"";
        if (!string.IsNullOrWhiteSpace(extraQuery)) query += $" {extraQuery.Trim()}";

        return query;
    }
}
