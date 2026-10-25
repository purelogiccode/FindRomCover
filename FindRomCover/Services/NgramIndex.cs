namespace FindRomCover.Services;

/// <summary>
///     Trigram inverted index used to quickly find image files whose names are similar
///     to a query.
/// </summary>
public class NgramIndex
{
    private const int N = 3;
    private const int MinTrigramMatches = 2;

    private readonly Dictionary<string, HashSet<string>> _trigramToFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the number of files added by the last <see cref="Build" /> call.</summary>
    public int FileCount { get; private set; }

    /// <summary>
    ///     Rebuilds the index from the supplied image file paths.
    /// </summary>
    /// <param name="imageFiles">The image file paths to index.</param>
    /// <param name="nameTransform">An optional transform applied to each file name before indexing.</param>
    public void Build(IEnumerable<string> imageFiles, Func<string, string>? nameTransform = null)
    {
        _trigramToFiles.Clear();
        FileCount = 0;

        foreach (var filePath in imageFiles)
        {
            FileCount++;
            var imageName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (nameTransform != null) imageName = nameTransform(imageName);
            var padded = new string(' ', N - 1) + imageName + new string(' ', N - 1);

            for (var i = 0; i <= padded.Length - N; i++)
            {
                var trigram = padded.Substring(i, N);
                if (!_trigramToFiles.TryGetValue(trigram, out var files))
                {
                    files = new HashSet<string>(AppConstants.FilePathComparer);
                    _trigramToFiles[trigram] = files;
                }

                files.Add(filePath);
            }
        }
    }

    /// <summary>
    ///     Returns indexed files that share at least two trigrams with the query.
    /// </summary>
    /// <param name="query">The name to find similar files for.</param>
    /// <returns>The candidate file paths, in unspecified order.</returns>
    public List<string> GetCandidates(string query)
    {
        if (FileCount == 0) return new List<string>();

        var padded = new string(' ', N - 1) + query.ToLowerInvariant() + new string(' ', N - 1);

        var queryTrigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i <= padded.Length - N; i++) queryTrigrams.Add(padded.Substring(i, N));

        if (queryTrigrams.Count == 0)
            return new List<string>();

        var fileMatchCounts = new Dictionary<string, int>(AppConstants.FilePathComparer);
        foreach (var trigram in queryTrigrams)
            if (_trigramToFiles.TryGetValue(trigram, out var files))
                foreach (var file in files)
                {
                    fileMatchCounts.TryAdd(file, 0);
                    fileMatchCounts[file]++;
                }

        return fileMatchCounts
            .Where(static kvp => kvp.Value >= MinTrigramMatches)
            .Select(static kvp => kvp.Key)
            .ToList();
    }
}