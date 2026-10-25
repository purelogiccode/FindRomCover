using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Vision model client that talks to the Google Gemini generateContent API.
/// </summary>
public sealed class GeminiVisionClient : VisionModelClientBase
{
    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GeminiVisionClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to send requests.</param>
    public GeminiVisionClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    ///     Sends a generateContent request to the Gemini API and returns the model text.
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
            throw new AiProviderException(response.StatusCode, BuildHttpErrorMessage(response.StatusCode, responseText));

        return ExtractMessageContent(responseText);
    }

    /// <summary>
    ///     Builds the Gemini generateContent endpoint URL, stripping any "models/" prefix from the model identifier.
    /// </summary>
    /// <param name="baseUrl">The provider base URL.</param>
    /// <param name="model">The configured model identifier.</param>
    /// <returns>The absolute generateContent endpoint URL.</returns>
    internal static string BuildGenerateContentUrl(string baseUrl, string model)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        var modelId = model.Trim();
        if (modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            modelId = modelId["models/".Length..];

        if (string.IsNullOrEmpty(modelId))
            throw new InvalidOperationException("AI model is not configured.");

        return $"{baseUrl.Trim().TrimEnd('/')}/models/{modelId}:generateContent";
    }

    /// <summary>
    ///     Serializes the Gemini request payload with the system instruction and inline base64 JPEG images.
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

        return AiRequestParameters.ApplyToJson(
            JsonSerializer.Serialize(payload),
            options.RequestParametersJson);
    }

    /// <summary>
    ///     Extracts the concatenated text from a Gemini response, throwing when the request was blocked or produced no content.
    /// </summary>
    /// <param name="responseJson">The raw JSON response body.</param>
    /// <returns>The model text.</returns>
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
}
