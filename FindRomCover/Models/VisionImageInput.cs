namespace FindRomCover.Models;

/// <summary>
///     A prepared image candidate sent to an AI vision model.
/// </summary>
/// <param name="Name">Display name used to reference the image in the AI prompt.</param>
/// <param name="Image">Prepared image payload.</param>
/// <param name="SourceIndex">Zero-based index of the source candidate this input was built from.</param>
public sealed record VisionImageInput(string Name, PreparedVisionImage Image, int SourceIndex);