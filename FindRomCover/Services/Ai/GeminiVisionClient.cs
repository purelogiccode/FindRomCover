using System.Net.Http;
using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class GeminiVisionClient : VisionModelClientBase
{
    private readonly HttpClient _httpClient;

    public GeminiVisionClient()
        : this(CreateDefaultHttpClient(), true)
    {
    }

    public GeminiVisionClient(HttpClient httpClient, bool ownsClient = false)
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

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildGenerateContentUrl(options.BaseUrl, options.Model));
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

    internal static string BuildGenerateContentUrl(string baseUrl, string model)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        var modelId = model?.Trim() ?? string.Empty;
        if (modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            modelId = modelId["models/".Length..];

        if (string.IsNullOrEmpty(modelId))
            throw new InvalidOperationException("AI model is not configured.");

        return $"{baseUrl.Trim().TrimEnd('/')}/models/{modelId}:generateContent";
    }

    internal static string BuildRequestBody(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images)
    {
        var parts = new List<object> { new { text = userPrompt } };

        foreach (var image in images)
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = "image/jpeg",
                    data = Convert.ToBase64String(image.Image.JpegBytes)
                }
            });

        var payload = new
        {
            systemInstruction = new { parts = new object[] { new { text = systemPrompt } } },
            contents = new object[] { new { role = "user", parts } },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 4096 }
        };

        return JsonSerializer.Serialize(payload);
    }

    internal static string ExtractMessageContent(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var blockReason) &&
            blockReason.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException($"AI provider blocked the request: {blockReason.GetString()}.");

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("AI response did not contain any candidates.");

        if (!candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("AI response did not contain any content.");

        var builder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                builder.Append(text.GetString());

        if (builder.Length == 0 &&
            candidates[0].TryGetProperty("finishReason", out var finishReason) &&
            finishReason.ValueKind == JsonValueKind.String &&
            string.Equals(finishReason.GetString(), "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
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
