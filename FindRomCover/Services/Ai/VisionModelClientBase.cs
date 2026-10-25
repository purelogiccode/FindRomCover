using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FindRomCover.Interfaces;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

/// <summary>
///     Base class for vision model clients that builds prompts, sends requests through a provider-specific
///     implementation, parses JSON verdicts, and normalizes provider errors.
/// </summary>
public abstract class VisionModelClientBase : IVisionModelClient
{
    /// <summary>
    ///     The maximum number of characters of provider error detail included in messages.
    /// </summary>
    internal const int MaxErrorDetailLength = 300;

    /// <summary>
    ///     The Anthropic API version header value sent with Anthropic requests.
    /// </summary>
    internal const string AnthropicVersion = "2023-06-01";

    /// <summary>
    ///     The message used when a model consumed its entire output budget without producing an answer.
    /// </summary>
    internal const string OutputBudgetExhaustedMessage =
        "AI model ran out of output tokens before producing an answer. Reasoning models can spend the whole " +
        "output budget on internal thinking; try a non-reasoning model (for example google/gemma-3-12b-it).";

    private const string PickSystemPrompt =
        "You are a retro video game cover-art expert. You receive a game title and numbered candidate images. " +
        "Choose the candidate that is the official cover or box art for that exact game. " +
        "If no candidate is genuine cover art for that exact game, use -1. " +
        "Reply with ONLY a JSON object: {\"bestIndex\": <int>, \"confidence\": <0.0-1.0>, \"reason\": \"<short reason>\"}.";

    private const string ApiFallbackPickSystemPrompt =
        "You are a retro video game cover-art expert. You receive a game title and numbered candidate images. " +
        "Choose the candidate that is the official cover or box art for that exact game. " +
        "Prefer a game cover or an in-game screenshot over a photo of the physical game cartridge, cart, or disc. " +
        "If no cover art is present, an in-game screenshot is acceptable. " +
        "If no candidate is cover art or a screenshot for that exact game, use -1. " +
        "Reply with ONLY a JSON object: {\"bestIndex\": <int>, \"confidence\": <0.0-1.0>, \"reason\": \"<short reason>\"}.";

    private const string VerifySystemPrompt =
        "You are a retro video game cover-art expert. Decide whether the image is cover art for the given game. " +
        "Reply with ONLY a JSON object: {\"match\": <true|false>, \"confidence\": <0.0-1.0>, \"reason\": \"<short reason>\"}.";

    /// <summary>
    ///     Asks the model to pick the best cover-art candidate for the given game.
    /// </summary>
    /// <param name="options">The AI vision options (provider, model, key, timeout).</param>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="images">The numbered candidate images.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="kind">Whether this is a local pick or an API fallback pick.</param>
    /// <returns>The pick verdict parsed from the model response.</returns>
    public Task<AiPickResult> PickBestAsync(
        AiVisionOptions options,
        string romName,
        string searchName,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken,
        AiPickKind kind = AiPickKind.Local)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
            throw new InvalidOperationException("At least one candidate image is required.");

        var systemPrompt = kind == AiPickKind.ApiFallback ? ApiFallbackPickSystemPrompt : PickSystemPrompt;
        return SendAsync(options, systemPrompt, BuildPickPrompt(romName, searchName, images), images,
            ParsePickResponse, cancellationToken);
    }

    /// <summary>
    ///     Asks the model whether the given image is cover art for the given game.
    /// </summary>
    /// <param name="options">The AI vision options (provider, model, key, timeout).</param>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="image">The prepared image to verify.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The verification verdict parsed from the model response.</returns>
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
            (string.IsNullOrWhiteSpace(searchName) ||
             string.Equals(searchName, romName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"Alternate title: {searchName}\n") +
            "Is the attached image cover art for this exact game?";

        return SendAsync(options, VerifySystemPrompt, prompt, [image], ParseVerificationResponse, cancellationToken);
    }

    /// <summary>
    ///     Releases resources held by the client.
    /// </summary>
    public void Dispose()
    {
        DisposeCore();
    }

    /// <summary>
    ///     Releases resources held by derived clients; the base implementation does nothing.
    /// </summary>
    protected virtual void DisposeCore()
    {
    }

    /// <summary>
    ///     Sends a provider-specific request and returns the raw assistant text.
    /// </summary>
    /// <param name="options">The AI vision options (provider, model, key, timeout).</param>
    /// <param name="systemPrompt">The system prompt guiding the model.</param>
    /// <param name="userPrompt">The user prompt describing the task.</param>
    /// <param name="images">The prepared candidate images to attach.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The raw text content returned by the provider.</returns>
    protected abstract Task<string> SendRequestAsync(
        AiVisionOptions options,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Builds the user prompt listing the game titles and numbered candidate images.
    /// </summary>
    /// <param name="romName">The ROM name used as the primary game title.</param>
    /// <param name="searchName">The search name used as an alternate title.</param>
    /// <param name="images">The numbered candidate images.</param>
    /// <returns>The pick user prompt.</returns>
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

    /// <summary>
    ///     Extracts the JSON verdict object from the model response. When the response
    ///     contains several valid JSON objects (for example an example object printed
    ///     before the real answer), the first object carrying verdict fields wins.
    /// </summary>
    /// <param name="content">The raw model response text.</param>
    /// <returns>The JSON object text.</returns>
    internal static string ExtractJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("AI response did not contain a JSON object.");

        // Models sometimes wrap the JSON in prose or a markdown fence, and reasoning
        // models can mention braces (or example JSON) before the real answer. Try every
        // balanced brace group: prefer one that carries verdict fields, otherwise fall
        // back to the first valid JSON object.
        string? firstValid = null;

        for (var start = content.IndexOf('{'); start >= 0; start = content.IndexOf('{', start + 1))
        {
            var end = FindMatchingBrace(content, start);
            if (end < 0) continue;

            var candidate = content[start..(end + 1)];
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    (root.TryGetProperty("bestIndex", out _) ||
                     root.TryGetProperty("isMatch", out _) ||
                     root.TryGetProperty("confidence", out _)))
                    return candidate;

                firstValid ??= candidate;
            }
            catch (JsonException)
            {
                // Not a JSON object (for example a brace inside prose); keep scanning.
            }
        }

        if (firstValid != null) return firstValid;

        throw new InvalidOperationException("AI response did not contain a JSON object.");
    }

    private static int FindMatchingBrace(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }

        return -1;
    }

    /// <summary>
    ///     Parses a pick verdict from the JSON object embedded in a model response.
    /// </summary>
    /// <param name="content">The raw model response text.</param>
    /// <returns>The parsed pick result with a normalized confidence.</returns>
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

    /// <summary>
    ///     Parses a verification verdict from the JSON object embedded in a model response.
    /// </summary>
    /// <param name="content">The raw model response text.</param>
    /// <returns>The parsed verification result with a normalized confidence.</returns>
    internal static AiVerificationResult ParseVerificationResponse(string content)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(content));
        var root = doc.RootElement;

        return new AiVerificationResult
        {
            // Accept the documented "match" key and the common "isMatch" variant. If the
            // model omitted the verdict entirely, do not silently reject the image: the
            // safe default for an unparseable answer is to proceed without verification.
            IsMatch = GetBool(root, "match") ?? GetBool(root, "isMatch") ?? true,
            Confidence = NormalizeConfidence(GetDouble(root, "confidence") ?? 0),
            Reason = GetString(root, "reason") ?? string.Empty
        };
    }

    /// <summary>
    ///     Builds a user-facing error message for a failed provider response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the provider.</param>
    /// <param name="responseBody">The raw error response body.</param>
    /// <returns>The formatted error message.</returns>
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

    /// <summary>
    ///     Extracts and truncates the provider error detail from a response body.
    /// </summary>
    /// <param name="responseBody">The raw error response body.</param>
    /// <returns>The provider error message, or the truncated raw body when it is not JSON.</returns>
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

    /// <summary>
    ///     Truncates error text to <see cref="MaxErrorDetailLength"/> characters.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <returns>The truncated text, or an empty string when the input is blank.</returns>
    internal static string Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return text.Length > MaxErrorDetailLength ? text[..MaxErrorDetailLength] + "..." : text;
    }

    /// <summary>
    ///     Applies provider-specific authentication and identification headers to a request.
    /// </summary>
    /// <param name="request">The request to add headers to.</param>
    /// <param name="options">The AI vision options providing the API key and provider.</param>
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
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"AI request timed out after {options.TimeoutSeconds} seconds. Check the AI settings or try a smaller model.",
                ex);
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
            // Accept integral reals too: models frequently emit {"bestIndex": 1.0}.
            // The range check keeps huge integral doubles (e.g. 1e20) from wrapping
            // around in the unchecked cast.
            JsonValueKind.Number when value.TryGetDouble(out var real) && Math.Abs(real - Math.Round(real)) < 0.0001
                                                        && real is >= int.MinValue and <= int.MaxValue
                => (int)Math.Round(real),
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float,
                                           CultureInfo.InvariantCulture, out var parsedReal) &&
                                       Math.Abs(parsedReal - Math.Round(parsedReal)) < 0.0001 &&
                                       parsedReal is >= int.MinValue and <= int.MaxValue => (int)Math.Round(
                parsedReal),
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
            JsonValueKind.String when string.Equals(value.GetString(), "yes", StringComparison.OrdinalIgnoreCase) =>
                true,
            JsonValueKind.String when string.Equals(value.GetString(), "no", StringComparison.OrdinalIgnoreCase) =>
                false,
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