using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public sealed class OpenAiCompatibleVisionClient : VisionModelClientBase
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleVisionClient()
        : this(CreateDefaultHttpClient(), true)
    {
    }

    public OpenAiCompatibleVisionClient(HttpClient httpClient, bool ownsClient = false)
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

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(options.BaseUrl));
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        ApplyAuthHeaders(request, options);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildErrorMessage(response.StatusCode, responseText));

        return ExtractMessageContent(responseText);
    }

    internal static string BuildRequestBody(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images)
    {
        var userContent = new List<object> { new { type = "text", text = userPrompt } };

        foreach (var image in images)
            userContent.Add(new
            {
                type = "image_url",
                image_url = new { url = "data:image/jpeg;base64," + Convert.ToBase64String(image.Image.JpegBytes) }
            });

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["model"] = options.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            }
        };

        if (IsOpenAiReasoningModel(options))
        {
            // OpenAI reasoning models (o-series, gpt-5*) reject "temperature" and
            // require "max_completion_tokens" instead of "max_tokens".
            payload["max_completion_tokens"] = 4096;
        }
        else
        {
            payload["temperature"] = 0.1;
            payload["max_tokens"] = 4096;
        }

        return JsonSerializer.Serialize(payload);
    }

    internal static bool IsOpenAiReasoningModel(AiVisionOptions options)
    {
        if (options.Provider is not (AppConstants.AiProviders.OpenAi or AppConstants.AiProviders.CustomOpenAi))
            return false;

        var modelId = (options.Model ?? string.Empty).Trim();
        var slash = modelId.LastIndexOf('/');
        if (slash >= 0) modelId = modelId[(slash + 1)..];

        return modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildChatCompletionsUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        return baseUrl.Trim().TrimEnd('/') + "/chat/completions";
    }

    internal static string ExtractMessageContent(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
            throw new InvalidOperationException("AI response did not contain any choices.");

        if (!choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
            throw new InvalidOperationException("AI response did not contain a message.");

        if (content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString() ?? string.Empty;
            if (text.Length == 0) ThrowIfOutputBudgetExhausted(choices[0]);

            return text;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            ThrowIfOutputBudgetExhausted(choices[0]);
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray())
            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                builder.Append(text.GetString());

        if (builder.Length == 0) ThrowIfOutputBudgetExhausted(choices[0]);

        return builder.ToString();
    }

    private static void ThrowIfOutputBudgetExhausted(JsonElement choice)
    {
        if (choice.TryGetProperty("finish_reason", out var finishReason) &&
            finishReason.ValueKind == JsonValueKind.String &&
            string.Equals(finishReason.GetString(), "length", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(OutputBudgetExhaustedMessage);
    }

    internal static string BuildErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        return BuildHttpErrorMessage(statusCode, responseBody);
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
