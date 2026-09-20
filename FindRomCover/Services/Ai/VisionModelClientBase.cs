using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

public abstract class VisionModelClientBase : IVisionModelClient
{
    internal const int MaxErrorDetailLength = 300;
    internal const string AnthropicVersion = "2023-06-01";

    internal const string OutputBudgetExhaustedMessage =
        "AI model ran out of output tokens before producing an answer. Reasoning models can spend the whole " +
        "output budget on internal thinking; try a non-reasoning model (for example google/gemma-3-12b-it).";

    private const string PickSystemPrompt =
        "You are a retro video game cover-art expert. You receive a game title and numbered candidate images. " +
        "Choose the candidate that is the official cover or box art for that exact game. " +
        "If no candidate is genuine cover art for that exact game, use -1. " +
        "Reply with ONLY a JSON object: {\"bestIndex\": <int>, \"confidence\": <0.0-1.0>, \"reason\": \"<short reason>\"}.";

    private const string VerifySystemPrompt =
        "You are a retro video game cover-art expert. Decide whether the image is cover art for the given game. " +
        "Reply with ONLY a JSON object: {\"match\": <true|false>, \"confidence\": <0.0-1.0>, \"reason\": \"<short reason>\"}.";

    public Task<AiPickResult> PickBestAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
            throw new InvalidOperationException("At least one candidate image is required.");

        return SendAsync(options, PickSystemPrompt, BuildPickPrompt(romName, searchName, images), images,
            ParsePickResponse, cancellationToken);
    }

    public Task<AiVerificationResult> VerifyAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        VisionImageInput image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(image);

        var prompt =
            $"Game title: {romName}\n" +
            (string.IsNullOrWhiteSpace(searchName) || string.Equals(searchName, romName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"Alternate title: {searchName}\n") +
            "Is the attached image cover art for this exact game?";

        return SendAsync(options, VerifySystemPrompt, prompt, [image], ParseVerificationResponse, cancellationToken);
    }

    public void Dispose()
    {
        DisposeCore();
    }

    protected virtual void DisposeCore()
    {
    }

    protected abstract Task<string> SendRequestAsync(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken);

    internal static string BuildPickPrompt(string romName, string searchName, IReadOnlyList<VisionImageInput> images)
    {
        var builder = new StringBuilder();
        builder.Append("Game title: ").AppendLine(romName);
        if (!string.IsNullOrWhiteSpace(searchName) &&
            !string.Equals(searchName, romName, StringComparison.OrdinalIgnoreCase))
            builder.Append("Alternate title: ").AppendLine(searchName);

        builder.AppendLine("Candidate images (in order):");
        for (var i = 0; i < images.Count; i++) builder.Append(i).Append(": ").AppendLine(images[i].Name);

        builder.Append("Which candidate is the official cover art for this exact game?");
        return builder.ToString();
    }

    internal static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{', StringComparison.Ordinal);
        var end = content.LastIndexOf('}');

        if (start < 0 || end <= start)
            throw new InvalidOperationException("AI response did not contain a JSON object.");

        return content[start..(end + 1)];
    }

    internal static AiPickResult ParsePickResponse(string content)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(content));
        var root = doc.RootElement;

        return new AiPickResult
        {
            BestIndex = GetInt(root, "bestIndex") ?? -1,
            Confidence = NormalizeConfidence(GetDouble(root, "confidence") ?? 0),
            Reason = GetString(root, "reason") ?? string.Empty
        };
    }

    internal static AiVerificationResult ParseVerificationResponse(string content)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(content));
        var root = doc.RootElement;

        return new AiVerificationResult
        {
            IsMatch = GetBool(root, "match") ?? false,
            Confidence = NormalizeConfidence(GetDouble(root, "confidence") ?? 0),
            Reason = GetString(root, "reason") ?? string.Empty
        };
    }

    internal static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        var detail = ExtractProviderError(responseBody);

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => $"AI provider rejected the API key (401). {detail}",
            HttpStatusCode.Forbidden => $"AI provider denied access (403). {detail}",
            HttpStatusCode.TooManyRequests => $"AI provider rate limit exceeded (429). {detail}",
            HttpStatusCode.NotFound =>
                $"AI model or endpoint was not found (404). Check the model name and Base URL. {detail}",
            _ => $"AI provider error ({(int)statusCode}). {detail}"
        };
    }

    internal static string ExtractProviderError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return Truncate(error.GetString());

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                    return Truncate(message.GetString());
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body; fall through to the raw text.
        }

        return Truncate(responseBody);
    }

    internal static string Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return text.Length > MaxErrorDetailLength ? text[..MaxErrorDetailLength] + "..." : text;
    }

    internal static void ApplyAuthHeaders(HttpRequestMessage request, AiVisionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            switch (options.Provider)
            {
                case AppConstants.AiProviders.Anthropic:
                case AppConstants.AiProviders.CustomAnthropic:
                    request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);
                    break;
                case AppConstants.AiProviders.Gemini:
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", options.ApiKey);
                    break;
                default:
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                    break;
            }

        if (options.Provider is AppConstants.AiProviders.Anthropic or AppConstants.AiProviders.CustomAnthropic)
            request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

        if (string.Equals(options.Provider, AppConstants.AiProviders.OpenRouter, StringComparison.Ordinal))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/purelogiccode/FindRomCover");
            request.Headers.TryAddWithoutValidation("X-Title", "FindRomCover");
        }
    }

    private async Task<T> SendAsync<T>(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        Func<string, T> parse,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));

        LogService.Debug(
            $"AI vision request: provider='{options.Provider}', model='{options.Model}', images={images.Count}");

        try
        {
            var content = await SendRequestAsync(options, systemPrompt, userPrompt, images, timeoutCts.Token)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("AI model returned an empty response.");

            return parse(content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"AI request timed out after {options.TimeoutSeconds} seconds. Check the AI settings or try a smaller model.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the AI provider at '{options.BaseUrl}'. {ex.Message}", ex);
        }
    }

    private static double NormalizeConfidence(double confidence)
    {
        if (confidence > 1 && confidence <= 100) confidence /= 100;
        return Math.Clamp(confidence, 0, 1);
    }

    private static int? GetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? GetBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.String when string.Equals(value.GetString(), "yes", StringComparison.OrdinalIgnoreCase) => true,
            JsonValueKind.String when string.Equals(value.GetString(), "no", StringComparison.OrdinalIgnoreCase) => false,
            _ => null
        };
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
