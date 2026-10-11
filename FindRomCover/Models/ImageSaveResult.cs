namespace FindRomCover.Models;

/// <summary>
///     Result of an image conversion/save operation performed by
///     <see cref="FindRomCover.Services.ImageProcessor" />.
/// </summary>
/// <param name="Success">Whether the image was converted and saved successfully.</param>
/// <param name="ErrorMessage">A user-friendly error description when <paramref name="Success" /> is false.</param>
/// <param name="ErrorTitle">A short dialog title for the failure.</param>
/// <param name="ErrorIcon">The icon to show in the failure dialog.</param>
/// <param name="Exception">The underlying exception, when one was raised.</param>
/// <param name="LogContext">Additional context written to the log on failure.</param>
public sealed record ImageSaveResult(
    bool Success,
    string? ErrorMessage = null,
    string? ErrorTitle = null,
    MessageIcon ErrorIcon = MessageIcon.None,
    Exception? Exception = null,
    string? LogContext = null);
