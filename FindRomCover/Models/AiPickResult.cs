namespace FindRomCover.Models;

public sealed class AiPickResult
{
    public int BestIndex { get; init; } = -1;
    public double Confidence { get; init; }
    public string Reason { get; init; } = string.Empty;

    public bool HasPick => BestIndex >= 0;
}
