namespace FindRomCover.Models;

public class MameDatCorruptError : Exception
{
    public MameDatCorruptError(string message, Exception innerException) : base(message, innerException)
    {
    }

    public MameDatCorruptError()
    {
    }

    public MameDatCorruptError(string? message) : base(message)
    {
    }
}