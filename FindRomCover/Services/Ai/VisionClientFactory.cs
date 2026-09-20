using System.Net.Http;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public static class VisionClientFactory
{
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    public static IVisionModelClient Create(AiVisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider switch
        {
            AppConstants.AiProviders.Anthropic => new AnthropicVisionClient(SharedHttpClient),
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
