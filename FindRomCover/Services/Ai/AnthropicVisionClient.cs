using System.Net.Http;
using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class AnthropicVisionClient : VisionModelClientBase
{
    private readonly HttpClient _httpClient;

    public AnthropicVisionClient()
        : this(CreateDefaultHttpClient(), true)
    {
    }

    public AnthropicVisionClient(HttpClient httpClient, bool ownsClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        OwnsClient = ownsClient;
    }

    public bool OwnsClient { get; }

    protected override void DisposeCore()
    {
        if (OwnsClient) _httpClient.Dispose();
    }

    protected override async Task<string> SendRequestAsync(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken)
    {
        var requestBody = BuildRequestBody(options, systemPrompt, userPrompt, images);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMessagesUrl(options.BaseUrl));
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        ApplyAuthHeaders(request, options);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildHttpErrorMessage(response.StatusCode, responseText));

        return ExtractMessageContent(responseText);
    }

    internal static string BuildMessagesUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        return baseUrl.Trim().TrimEnd('/') + "/messages";
    }

    internal static string BuildRequestBody(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images)
    {
        var content = new List<object> { new { type = "text", text = userPrompt } };

        foreach (var image in images)
            content.Add(new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = "image/jpeg",
                    data = Convert.ToBase64String(image.Image.JpegBytes)
                }
            });

        var payload = new
        {
            model = options.Model,
            max_tokens = 4096,
            temperature = 0.1,
            system = systemPrompt,
            messages = new object[] { new { role = "user", content } }
        };

        return JsonSerializer.Serialize(payload);
    }

    internal static string ExtractMessageContent(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("AI response did not contain any content.");

        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (!part.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                !string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase))
                continue;

            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                builder.Append(text.GetString());
        }

        if (builder.Length == 0 &&
            doc.RootElement.TryGetProperty("stop_reason", out var stopReason) &&
            stopReason.ValueKind == JsonValueKind.String &&
            string.Equals(stopReason.GetString(), "max_tokens", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(OutputBudgetExhaustedMessage);

        return builder.ToString();
    }

    private static HttpClient CreateDefaultHttpClient()
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
