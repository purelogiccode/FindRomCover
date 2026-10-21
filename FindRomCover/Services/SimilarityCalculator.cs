using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FindRomCover.Managers;
using FindRomCover.Models;

namespace FindRomCover.Services;

/// <summary>
///     Computes filename similarity scores with the configured algorithm and loads the
///     best matching cover images for display.
/// </summary>
[SuppressMessage("ReSharper", "NullableWarningSuppressionIsUsed")]
public static class SimilarityCalculator
{
    /// <summary>Default maximum number of candidate images loaded for display.</summary>
    public const int DefaultMaxImagesToLoad = 30;

    private const int NgramIndexMinFileCount = 50;
    private const double NgramIndexFallbackRatio = 0.05;

    /// <summary>
    ///     Finds images whose names are similar to the selected file and loads the best
    ///     matches for display.
    /// </summary>
    /// <param name="selectedFileName">The file name to compare against.</param>
    /// <param name="imageFolderPath">The folder containing candidate images.</param>
    /// <param name="similarityThreshold">Minimum similarity percentage required for a match.</param>
    /// <param name="algorithm">The algorithm used to score similarity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="maxImagesToLoad">Maximum number of images to load; 0 uses the configured default.</param>
    /// <param name="onImageLoaded">Optional callback invoked for each successfully loaded image.</param>
    /// <param name="ignoreBracketedText">True to strip bracketed annotations from names before comparing.</param>
    /// <returns>The similarity result containing matched images and processing errors.</returns>
    public static async Task<SimilarityCalculationResult> CalculateSimilarityAsync(
        string selectedFileName,
        string imageFolderPath,
        double similarityThreshold,
        string algorithm,
        CancellationToken cancellationToken,
        int maxImagesToLoad = 0,
        Action<ImageData>? onImageLoaded = null,
        bool ignoreBracketedText = false)
    {
        var result = new SimilarityCalculationResult();

        if (maxImagesToLoad <= 0) maxImagesToLoad = GetConfiguredMaxImagesToLoad();

        var (topCandidates, processingErrors) = await FindCandidatesCoreAsync(
            selectedFileName,
            imageFolderPath,
            similarityThreshold,
            algorithm,
            ignoreBracketedText,
            maxImagesToLoad,
            cancellationToken);

        var imageList = new ConcurrentBag<ImageData>();

        try
        {
            var ioParallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Math.Max(1, Environment.ProcessorCount - 1), 4),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(topCandidates, ioParallelOptions, async (candidate, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var (maxRetries, retryDelayMilliseconds) = GetConfiguredImageLoaderSettings();
                    var imageBytes = await ImageLoader.LoadImageToMemoryAsync(
                        candidate.FilePath,
                        ct,
                        maxRetries,
                        retryDelayMilliseconds);

                    if (imageBytes == null)
                    {
                        processingErrors.Add(
                            $"Image '{Path.GetFileName(candidate.FilePath)}' could not be loaded (corrupted or empty).");
                        return;
                    }

                    var imageData = new ImageData(candidate.FilePath, candidate.ImageName, candidate.SimilarityScore)
                    {
                        ImageSource = ImageSources.FromPngBytes(imageBytes)
                    };
                    imageList.Add(imageData);
                    onImageLoaded?.Invoke(imageData);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    processingErrors.Add(
                        $"Could not load image '{Path.GetFileName(candidate.FilePath)}' for display: {ex.Message}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            processingErrors.Add($"An unexpected error occurred during image loading for display: {ex.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        result.SimilarImages = imageList
            .OrderByDescending(static x => x.SimilarityScore)
            .ThenBy(static x => x.ImagePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.ProcessingErrors = processingErrors.ToList();

        return result;
    }

    /// <summary>
    ///     Finds the top matching images for the selected file without loading them for display.
    /// </summary>
    /// <param name="selectedFileName">The file name to compare against.</param>
    /// <param name="imageFolderPath">The folder containing candidate images.</param>
    /// <param name="similarityThreshold">Minimum similarity percentage required for a match.</param>
    /// <param name="algorithm">The algorithm used to score similarity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="maxCandidates">Maximum number of candidates to return.</param>
    /// <param name="ignoreBracketedText">True to strip bracketed annotations from names before comparing.</param>
    /// <returns>The matching candidates as (file path, image name, similarity score) tuples.</returns>
    public static async Task<List<(string FilePath, string ImageName, double SimilarityScore)>> FindTopCandidatesAsync(
        string selectedFileName,
        string imageFolderPath,
        double similarityThreshold,
        string algorithm,
        CancellationToken cancellationToken,
        int maxCandidates,
        bool ignoreBracketedText = false)
    {
        var (candidates, errors) = await FindCandidatesCoreAsync(
            selectedFileName,
            imageFolderPath,
            similarityThreshold,
            algorithm,
            ignoreBracketedText,
            maxCandidates,
            cancellationToken);

        if (errors.Count > 0)
            LogService.Debug($"FindTopCandidatesAsync: {errors.Count} issue(s) while scanning images.");

        return candidates;
    }

    private static async Task<(List<(string FilePath, string ImageName, double SimilarityScore)> Candidates,
        List<string>
        Errors)> FindCandidatesCoreAsync(
        string selectedFileName,
        string imageFolderPath,
        double similarityThreshold,
        string algorithm,
        bool ignoreBracketedText,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var processingErrors = new ConcurrentBag<string>();

        if (string.IsNullOrEmpty(imageFolderPath) || !Directory.Exists(imageFolderPath))
            return ([], processingErrors.ToList());

        if (!string.Equals(algorithm, AppConstants.Algorithms.Levenshtein, StringComparison.Ordinal) &&
            !string.Equals(algorithm, AppConstants.Algorithms.Jaccard, StringComparison.Ordinal) &&
            !string.Equals(algorithm, AppConstants.Algorithms.JaroWinkler, StringComparison.Ordinal))
            return ([], [$"Algorithm '{algorithm}' is not implemented."]);

        var effectiveMaxCandidates = Math.Max(0, maxCandidates);
        if (effectiveMaxCandidates == 0) return ([], []);

        if (ignoreBracketedText) selectedFileName = StripAnnotations(selectedFileName);

        string[] imageExtensions =
        [
            "*.png", "*.apng", "*.jpg", "*.jpeg", "*.jfif", "*.jpe", "*.avif", "*.bmp", "*.gif", "*.webp", "*.tiff",
            "*.tif", "*.ico", "*.svg", "*.jxl", "*.jp2", "*.heic", "*.heif"
        ];

        List<string> allImageFiles;
        try
        {
            allImageFiles = imageExtensions
                .SelectMany(ext => Directory.EnumerateFiles(imageFolderPath, ext))
                .ToList();
        }
        catch (Exception ex)
        {
            return ([], [$"Could not enumerate the image folder '{imageFolderPath}': {ex.Message}"]);
        }

        if (allImageFiles.Count == 0) return ([], processingErrors.ToList());

        HashSet<string>? jaccardQueryUnigrams = null;
        HashSet<string>? jaccardQueryBigrams = null;

        var filesToProcess = allImageFiles;
        if (allImageFiles.Count > NgramIndexMinFileCount)
        {
            var index = new NgramIndex();
            index.Build(allImageFiles, ignoreBracketedText ? StripAnnotations : null);
            var candidates = index.GetCandidates(selectedFileName);

            if (candidates.Count > 0 && candidates.Count >= allImageFiles.Count * NgramIndexFallbackRatio)
                filesToProcess = candidates;
        }

        var maxParallelism = Math.Max(1, Environment.ProcessorCount - 1);

        if (filesToProcess.Count > 5000) maxParallelism = Math.Min(maxParallelism, 4);

        var candidateFiles = new ConcurrentBag<(string FilePath, string ImageName, double SimilarityScore)>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism,
            CancellationToken = cancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(filesToProcess, parallelOptions, (imageFile, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var imageName = Path.GetFileNameWithoutExtension(imageFile);
                    var comparableImageName = ignoreBracketedText ? StripAnnotations(imageName) : imageName;

                    double similarityScore;
                    switch (algorithm)
                    {
                        case AppConstants.Algorithms.Levenshtein:
                            similarityScore =
                                CalculateLevenshteinSimilarity(selectedFileName, comparableImageName,
                                    similarityThreshold);
                            break;
                        case AppConstants.Algorithms.Jaccard:
                            var ngramSize =
                                Math.Min(selectedFileName.Length, comparableImageName.Length) < 2 ? 1 : 2;
                            var queryNgrams = ngramSize == 1
                                ? jaccardQueryUnigrams ??= GetNgrams(selectedFileName.ToLowerInvariant(), 1)
                                : jaccardQueryBigrams ??= GetNgrams(selectedFileName.ToLowerInvariant(), 2);
                            similarityScore = CalculateJaccardIndex(queryNgrams, comparableImageName, ngramSize);
                            break;
                        case AppConstants.Algorithms.JaroWinkler:
                            similarityScore = CalculateJaroWinklerDistance(selectedFileName, comparableImageName);
                            break;
                        default:
                            // Algorithms are validated before the loop; this is unreachable.
                            return ValueTask.CompletedTask;
                    }

                    if (similarityScore >= similarityThreshold)
                        candidateFiles.Add((imageFile, imageName, similarityScore));
                }
                catch (Exception ex)
                {
                    processingErrors.Add(
                        $"Could not process image '{Path.GetFileName(imageFile)}' for similarity: {ex.Message}");
                }

                return ValueTask.CompletedTask;
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            processingErrors.Add($"An unexpected error occurred during image file scanning: {ex.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Similarity scores tie frequently (especially with Jaro-Winkler). The parallel
        // workers fill the bag in a nondeterministic completion order, so a secondary
        // ordering key is required for stable results across runs.
        var topCandidates = candidateFiles
            .OrderByDescending(static x => x.SimilarityScore)
            .ThenBy(static x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(effectiveMaxCandidates)
            .ToList();

        return (topCandidates, processingErrors.ToList());
    }

    private static int GetConfiguredMaxImagesToLoad()
    {
        try
        {
            return SettingsManager.CurrentInstance?.MaxImagesToLoad ?? DefaultMaxImagesToLoad;
        }
        catch
        {
            return DefaultMaxImagesToLoad;
        }
    }

    private static (int MaxRetries, int RetryDelayMilliseconds) GetConfiguredImageLoaderSettings()
    {
        try
        {
            var settings = SettingsManager.CurrentInstance;
            return settings != null
                ? (settings.ImageLoaderMaxRetries, settings.ImageLoaderRetryDelayMilliseconds)
                : (ImageLoader.DefaultMaxRetries, ImageLoader.DefaultRetryDelayMilliseconds);
        }
        catch
        {
            return (ImageLoader.DefaultMaxRetries, ImageLoader.DefaultRetryDelayMilliseconds);
        }
    }

    /// <summary>
    ///     Removes bracketed annotations from a filename so that region, language, version, and other
    ///     metadata tags are ignored during matching. Handles parentheses, square brackets, curly braces,
    ///     and nested groups of the same type.
    /// </summary>
    /// <param name="fileName">The filename to clean.</param>
    /// <returns>The filename with balanced bracketed groups removed, or the original name when nothing remains.</returns>
    public static string StripAnnotations(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return fileName;

        var result = RemoveBalancedBracketGroups(fileName, '(', ')');
        result = RemoveBalancedBracketGroups(result, '[', ']');
        result = RemoveBalancedBracketGroups(result, '{', '}');
        result = result.Trim().TrimEnd('.', '_', ' ');

        return string.IsNullOrWhiteSpace(result) ? fileName : result;
    }

    private static string RemoveBalancedBracketGroups(string input, char open, char close)
    {
        if (input.IndexOf(open) < 0) return input;

        var result = new StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == open)
            {
                var depth = 1;
                var j = i + 1;
                while (j < input.Length && depth > 0)
                {
                    if (input[j] == open)
                        depth++;
                    else if (input[j] == close) depth--;

                    j++;
                }

                if (depth == 0)
                {
                    // Strip preceding whitespace before the bracket group
                    while (result.Length > 0 && (result[^1] == ' ' || result[^1] == '\t')) result.Length--;

                    i = j;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    ///     Calculates the Levenshtein similarity percentage between two names.
    /// </summary>
    /// <param name="a">The first string to compare.</param>
    /// <param name="b">The second string to compare.</param>
    /// <param name="similarityThreshold">Optional early-exit threshold (0-100); 0 disables the shortcut.</param>
    /// <returns>The similarity as a percentage from 0 to 100.</returns>
    internal static double CalculateLevenshteinSimilarity(string a, string b, double similarityThreshold = 0)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var lengthA = a.Length;
        var lengthB = b.Length;

        if (lengthA == 0 && lengthB == 0)
            return 100;

        var maxLength = Math.Max(lengthA, lengthB);

        // similarityThreshold is 0-100 (percentage). Convert to max allowed edit distance.
        // e.g., threshold=70 means allow up to 30% of the string length as edits. The
        // epsilon keeps mathematically integral values (for example 10% of 10 chars)
        // from truncating down and rejecting matches that score exactly at the threshold.
        var maxAllowedDistance = similarityThreshold > 0
            ? (int)Math.Floor((1.0 - similarityThreshold / 100.0) * maxLength + 1e-9)
            : int.MaxValue;

        var previousRow = new int[lengthB + 1];
        var currentRow = new int[lengthB + 1];

        for (var j = 0; j <= lengthB; j++) previousRow[j] = j;

        for (var i = 1; i <= lengthA; i++)
        {
            currentRow[0] = i;
            var minInRow = currentRow[0];

            for (var j = 1; j <= lengthB; j++)
            {
                var cost = b[j - 1] == a[i - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(previousRow[j] + 1, currentRow[j - 1] + 1),
                    previousRow[j - 1] + cost);

                if (currentRow[j] < minInRow) minInRow = currentRow[j];
            }

            if (maxAllowedDistance != int.MaxValue && minInRow > maxAllowedDistance)
                return 0;

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        var levenshteinDistance = previousRow[lengthB];
        var similarity = (1.0 - levenshteinDistance / (double)Math.Max(a.Length, b.Length)) * 100;
        return Math.Round(similarity, 2);
    }

    /// <summary>
    ///     Calculates the Jaccard index percentage between a prepared n-gram set and another string.
    /// </summary>
    /// <param name="setA">The pre-computed n-gram set of the first string.</param>
    /// <param name="b">The second string to compare.</param>
    /// <param name="ngramSize">The n-gram size used to tokenize <paramref name="b" />.</param>
    /// <returns>The similarity as a percentage from 0 to 100.</returns>
    internal static double CalculateJaccardIndex(HashSet<string> setA, string b, int ngramSize)
    {
        var setB = GetNgrams(b.ToLowerInvariant(), ngramSize);
        return ComputeJaccardFromSets(setA, setB);
    }

    private static double ComputeJaccardFromSets(HashSet<string> setA, HashSet<string> setB)
    {
        if (setA.Count == 0 && setB.Count == 0)
            return 100;

        var intersection = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        intersection.IntersectWith(setB);

        var union = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(setB);

        return union.Count == 0 ? 100 : intersection.Count / (double)union.Count * 100;
    }

    /// <summary>
    ///     Builds the set of overlapping n-grams for the input, padded with spaces at both ends.
    /// </summary>
    /// <param name="input">The string to tokenize.</param>
    /// <param name="n">The n-gram size.</param>
    /// <returns>The set of n-grams, or an empty set for empty input or non-positive sizes.</returns>
    internal static HashSet<string> GetNgrams(string input, int n)
    {
        if (string.IsNullOrEmpty(input) || n <= 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ngrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var padded = new string(' ', n - 1) + input + new string(' ', n - 1);

        for (var i = 0; i <= padded.Length - n; i++)
        {
            var ngram = padded.Substring(i, n);
            ngrams.Add(ngram);
        }

        return ngrams;
    }

    /// <summary>
    ///     Calculates the Jaro-Winkler similarity percentage between two names.
    /// </summary>
    /// <param name="s1">The first string to compare.</param>
    /// <param name="s2">The second string to compare.</param>
    /// <returns>The similarity as a percentage from 0 to 100.</returns>
    internal static double CalculateJaroWinklerDistance(string s1, string s2)
    {
        const double scalingFactor = 0.1;

        s1 = s1.ToLowerInvariant();
        s2 = s2.ToLowerInvariant();

        var s1Len = s1.Length;
        var s2Len = s2.Length;

        if (s1Len == 0 && s2Len == 0) return 100.0;

        if (s1Len == 0 || s2Len == 0) return 0.0;

        var matchDistance = Math.Max(0, Math.Max(s1Len, s2Len) / 2 - 1);

        var s1Matches = ArrayPool<bool>.Shared.Rent(s1Len);
        var s2Matches = ArrayPool<bool>.Shared.Rent(s2Len);

        try
        {
            Array.Clear(s1Matches, 0, s1Len);
            Array.Clear(s2Matches, 0, s2Len);

            var matches = 0;
            var transpositions = 0;

            for (var i = 0; i < s1Len; i++)
            {
                var start = Math.Max(0, i - matchDistance);
                var end = Math.Min(i + matchDistance + 1, s2Len);

                for (var j = start; j < end; j++)
                {
                    if (s2Matches[j]) continue;
                    if (s1[i] != s2[j]) continue;

                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0;

            var k = 0;
            for (var i = 0; i < s1Len; i++)
            {
                if (!s1Matches[i]) continue;

                while (!s2Matches[k]) k++;

                if (s1[i] != s2[k]) transpositions++;

                k++;
            }

            var jaro =
                ((double)matches / s1Len + (double)matches / s2Len + (matches - (double)transpositions / 2) / matches) /
                3;

            var prefixLength = 0;
            for (var i = 0; i < Math.Min(s1Len, s2Len); i++)
                if (s1[i] == s2[i])
                {
                    prefixLength++;
                    if (prefixLength == 4) break;
                }
                else
                {
                    break;
                }

            var jaroWinkler = jaro + prefixLength * scalingFactor * (1 - jaro);
            jaroWinkler = Math.Min(jaroWinkler, 1.0);
            return jaroWinkler * 100;
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(s1Matches);
            ArrayPool<bool>.Shared.Return(s2Matches);
        }
    }
}