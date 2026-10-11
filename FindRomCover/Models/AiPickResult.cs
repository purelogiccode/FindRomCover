namespace FindRomCover.Models;

/// <summary>
///     The AI vision model's choice of the best candidate image.
/// </summary>
public sealed class AiPickResult
{
    /// <summary>Zero-based index of the chosen candidate, or -1 when the model made no pick.</summary>
    public int BestIndex { get; init; } = -1;
    /// <summary>Confidence reported by the model, between 0 and 1.</summary>
    public double Confidence { get; init; }
    /// <summary>Short explanation of the choice.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Whether the model selected a candidate (<see cref="BestIndex" /> is non-negative).</summary>
    public bool HasPick => BestIndex >= 0;
}
