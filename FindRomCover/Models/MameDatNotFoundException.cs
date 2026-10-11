namespace FindRomCover.Models;

/// <summary>
///     Thrown when the required mame.dat data file cannot be found.
/// </summary>
public class MameDatNotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatNotFoundException" /> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MameDatNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatNotFoundException" /> class.
    /// </summary>
    public MameDatNotFoundException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MameDatNotFoundException" /> class with an error
    ///     message and the exception that caused it.
    /// </summary>
    /// <param name="message">The error message, or <see langword="null" /> for a default message.</param>
    /// <param name="innerException">The exception that caused this error, or <see langword="null" />.</param>
    public MameDatNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
