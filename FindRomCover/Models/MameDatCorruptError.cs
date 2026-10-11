namespace FindRomCover.Models;

/// <summary>
///     Thrown when the mame.dat data file is corrupted or in an invalid format.
/// </summary>
public class MameDatCorruptError : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatCorruptError" /> class with an error
    ///     message and the exception that caused it.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public MameDatCorruptError(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatCorruptError" /> class.
    /// </summary>
    public MameDatCorruptError()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatCorruptError" /> class with an error message.
    /// </summary>
    /// <param name="message">The error message, or <see langword="null" /> for a default message.</param>
    public MameDatCorruptError(string? message) : base(message)
    {
    }
}
