namespace FindRomCover.Models;

public class MameDatNotFoundException : Exception
{
    public MameDatNotFoundException(string message) : base(message)
    {
    }

    public MameDatNotFoundException()
    {
    }

    public MameDatNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}