namespace FindRomCover.Models;

/// <summary>
///     Result of a message dialog shown with <see cref="FindRomCover.Services.Dialogs" />.
/// </summary>
public enum MessageResult
{
    /// <summary>No button was pressed (dialog was closed).</summary>
    None,

    /// <summary>The OK button was pressed.</summary>
    Ok,

    /// <summary>The Yes button was pressed.</summary>
    Yes,

    /// <summary>The No button was pressed.</summary>
    No,

    /// <summary>The Cancel button was pressed.</summary>
    Cancel
}
