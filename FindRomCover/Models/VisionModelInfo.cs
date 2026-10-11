namespace FindRomCover.Models;

/// <summary>
///     Describes a model exposed by an AI provider.
/// </summary>
/// <param name="Id">Model identifier.</param>
/// <param name="IsVisionCapable">Whether the model accepts image input.</param>
public sealed record VisionModelInfo(string Id, bool IsVisionCapable);
