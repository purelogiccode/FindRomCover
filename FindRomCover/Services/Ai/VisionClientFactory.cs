using FindRomCover.Interfaces;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Creates the appropriate vision model client for the configured AI provider.
/// </summary>
public static class VisionClientFactory
{
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    /// <summary>
    ///     Creates a vision model client matching the provider configured in the options.
    /// </summary>
    /// <param name="options">The AI vision options whose provider selects the client implementation.</param>
    /// <returns>A client instance backed by a shared HTTP client.</returns>
    public static IVisionModelClient Create(AiVisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider switch
        {
            AppConstants.AiProviders.Anthropic => new AnthropicVisionClient(SharedHttpClient),
            AppConstants.AiProviders.CustomAnthropic => new AnthropicVisionClient(SharedHttpClient),
            AppConstants.AiProviders.Gemini => new GeminiVisionClient(SharedHttpClient),
            _ => new OpenAiCompatibleVisionClient(SharedHttpClient)
        };
    }

    private static HttpClient CreateSharedHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 4
        };

        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
