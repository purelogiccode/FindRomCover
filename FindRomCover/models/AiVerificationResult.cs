namespace FindRomCover.Models;

public sealed class AiVerificationResult
{
    public bool IsMatch { get; init; }
    public double Confidence { get; init; }
    public string Reason { get; init; } = string.Empty;
}
