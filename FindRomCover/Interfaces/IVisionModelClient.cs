using FindRomCover.Models;

namespace FindRomCover.Interfaces;

/// <summary>
///     Abstraction over a vision-capable AI provider used to pick the best cover
///     image for a ROM and to verify a candidate image.
/// </summary>
public interface IVisionModelClient : IDisposable
{
    /// <summary>
    ///     Asks the model to pick the best matching image for the given game.
    /// </summary>
    /// <param name="options">The AI provider configuration to use.</param>
    /// <param name="romName">The ROM/game name.</param>
    /// <param name="searchName">The cleaned search name used for querying.</param>
    /// <param name="images">The candidate images, in ranked order.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <param name="kind">Whether the candidates are local files or remote URLs.</param>
    /// <returns>The pick result, or <see langword="null" /> when the model returned nothing usable.</returns>
    Task<AiPickResult> PickBestAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken,
        AiPickKind kind = AiPickKind.Local);

    /// <summary>
    ///     Asks the model to verify that an image actually matches the given game.
    /// </summary>
    /// <param name="options">The AI provider configuration to use.</param>
    /// <param name="romName">The ROM/game name.</param>
    /// <param name="searchName">The cleaned search name used for querying.</param>
    /// <param name="image">The candidate image to verify.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The verification result (match flag, confidence and reason).</returns>
    Task<AiVerificationResult> VerifyAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        VisionImageInput image,
        CancellationToken cancellationToken);
}