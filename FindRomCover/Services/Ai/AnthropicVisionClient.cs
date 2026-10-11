using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Vision model client that talks to the Anthropic Messages API.
/// </summary>
public sealed class AnthropicVisionClient : VisionModelClientBase
{
    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new instance using a dedicated default HTTP client owned by this instance.
    /// </summary>
    public AnthropicVisionClient()
        : this(CreateDefaultHttpClient(), true)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnthropicVisionClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    /// <param name="ownsClient">Whether this instance disposes <paramref name="httpClient"/>.</param>
    public AnthropicVisionClient(HttpClient httpClient, bool ownsClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        OwnsClient = ownsClient;
    }

    /// <summary>
    ///     Gets a value indicating whether this instance disposes the underlying HTTP client.
    /// </summary>
    public bool OwnsClient { get; }

    /// <summary>
    ///     Disposes the underlying HTTP client when this instance owns it.
    /// </summary>
    protected override void DisposeCore()
    {
        if (OwnsClient) _httpClient.Dispose();
    }

    /// <summary>
    ///     Sends a request to the Anthropic Messages API and returns the assistant text.
    /// </summary>
    /// <param name="options">The AI vision options (base URL, model, key, timeout).</param>
    /// <param name="systemPrompt">The system prompt guiding the model.</param>
    /// <param name="userPrompt">The user prompt describing the task.</param>
    /// <param name="images">The prepared candidate images to attach.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The concatenated text content of the response.</returns>
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

    /// <summary>
    ///     Builds the Anthropic messages endpoint URL from the configured base URL.
    /// </summary>
    /// <param name="baseUrl">The provider base URL.</param>
    /// <returns>The absolute messages endpoint URL.</returns>
    internal static string BuildMessagesUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        return baseUrl.Trim().TrimEnd('/') + "/messages";
    }

    /// <summary>
    ///     Serializes the Anthropic request payload, embedding each image as base64 JPEG data.
    /// </summary>
    /// <param name="options">The AI vision options providing the model name.</param>
    /// <param name="systemPrompt">The system prompt guiding the model.</param>
    /// <param name="userPrompt">The user prompt describing the task.</param>
    /// <param name="images">The prepared candidate images to attach.</param>
    /// <returns>The JSON request body.</returns>
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

    /// <summary>
    ///     Extracts the concatenated text content from an Anthropic response body.
    /// </summary>
    /// <param name="responseJson">The raw JSON response body.</param>
    /// <returns>The assistant text.</returns>
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
