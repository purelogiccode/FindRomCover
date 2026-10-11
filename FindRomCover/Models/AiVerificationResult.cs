namespace FindRomCover.Models;

/// <summary>
///     Result of asking the AI vision model to verify a saved cover image.
/// </summary>
public sealed class AiVerificationResult
{
    /// <summary>Whether the model considers the image a match for the ROM.</summary>
    public bool IsMatch { get; init; }

    /// <summary>Confidence reported by the model, between 0 and 1.</summary>
    public double Confidence { get; init; }

    /// <summary>Short explanation of the verdict.</summary>
    public string Reason { get; init; } = string.Empty;
}