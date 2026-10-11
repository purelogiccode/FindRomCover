namespace FindRomCover.Models;

/// <summary>
///     Result of scanning a folder for images similar to a search name.
/// </summary>
public class SimilarityCalculationResult
{
    /// <summary>Images that matched the search name.</summary>
    public List<ImageData> SimilarImages { get; set; } = new();
    /// <summary>Errors encountered while processing individual files.</summary>
    public List<string> ProcessingErrors { get; set; } = new();
}
