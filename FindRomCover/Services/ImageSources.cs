namespace FindRomCover.Services;

/// <summary>
///     UI-specific image creation bridge for the platform-agnostic core.
///     Each GUI head (WPF, Avalonia) installs its factories at startup so that
///     <see cref="FindRomCover.Models.ImageData" /> and other core services can produce
///     displayable images without referencing a UI framework.
/// </summary>
public static class ImageSources
{
    private static readonly Lock BrokenImageLock = new();

    /// <summary>Creates a displayable image object from PNG bytes (UI-specific type).</summary>
    public static Func<byte[], object?>? CreateFromPngBytes { get; set; }

    /// <summary>Creates the broken-image placeholder used when no image is available.</summary>
    public static Func<object?>? CreateBrokenImage { get; set; }

    /// <summary>
    ///     Creates a displayable image from PNG bytes using the installed factory.
    /// </summary>
    /// <param name="pngBytes">The PNG-encoded image data.</param>
    /// <returns>The displayable image, or null when no factory is installed.</returns>
    public static object? FromPngBytes(byte[] pngBytes)
    {
        return CreateFromPngBytes?.Invoke(pngBytes);
    }

    /// <summary>
    ///     Gets the broken-image placeholder produced by the installed factory, if any.
    ///     The bitmap is created once and shared by every item that has no image yet.
    /// </summary>
    public static object? BrokenImage
    {
        get
        {
            if (field != null) return field;

            lock (BrokenImageLock)
            {
                return field ??= CreateBrokenImage?.Invoke();
            }
        }
    }
}
