using System.Net;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Raised when an AI provider answers with a non-success HTTP status. Carries the
///     status code so the shared client base can retry failures that are only temporary
///     (upstream gateway errors, rate limits and timeouts) before surfacing them to the user.
/// </summary>
public sealed class AiProviderException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    public AiProviderException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    /// <param name="message">The user-facing error message.</param>
    public AiProviderException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    /// <param name="message">The user-facing error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public AiProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the provider.</param>
    /// <param name="message">The user-facing error message.</param>
    public AiProviderException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = (int)statusCode;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiProviderException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the provider.</param>
    /// <param name="message">The user-facing error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public AiProviderException(HttpStatusCode statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = (int)statusCode;
    }

    /// <summary>
    ///     Gets the HTTP status code returned by the provider, or 0 when it is unknown.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    ///     Gets a value indicating whether the failure is temporary and worth retrying.
    ///     Gateways (502/503/504) and rate limits (429) routinely succeed on a retry;
    ///     authentication and request errors do not.
    /// </summary>
    public bool IsTransient => StatusCode is 408 or 425 or 429 or 500 or 502 or 503 or 504;
}
