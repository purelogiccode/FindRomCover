namespace FindRomCover.Interfaces;

/// <summary>
///     Plays user-interface sounds. Implementations may be silent when the host
///     platform has no usable audio device.
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>
    ///     Plays the short click sound used by interactive controls.
    /// </summary>
    void PlayClickSound();
}