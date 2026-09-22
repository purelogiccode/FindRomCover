using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public interface IVisionModelClient : IDisposable
{
    Task<AiPickResult> PickBestAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken,
        AiPickKind kind = AiPickKind.Local);

    Task<AiVerificationResult> VerifyAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        VisionImageInput image,
        CancellationToken cancellationToken);
}
