using System.Net.Http;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

internal static class VisionModelCatalog
{
    public static async Task<List<VisionModelInfo>> FetchModelsAsync(
        AiVisionOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsUrl(options.BaseUrl));
        VisionModelClientBase.ApplyAuthHeaders(request, options);

        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Connection test timed out after {options.TimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the AI provider at '{options.BaseUrl}'. {ex.Message}", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(VisionModelClientBase.BuildHttpErrorMessage(response.StatusCode, body));

            return ParseModels(body);
        }
    }

    public static async Task<List<string>> FetchModelIdsAsync(
        AiVisionOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var models = await FetchModelsAsync(options, httpClient, cancellationToken).ConfigureAwait(false);
        return models.Select(static model => model.Id).ToList();
    }

    internal static string BuildModelsUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        return baseUrl.Trim().TrimEnd('/') + "/models";
    }

    internal static List<string> ParseModelIds(string json)
    {
        return ParseModels(json).Select(static model => model.Id).ToList();
    }

    internal static List<VisionModelInfo> ParseModels(string json)
    {
        var models = new List<VisionModelInfo>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                AddModel(models, id, ReadVisionFlag(item) ?? LooksVisionCapable(id));
            }

        if (root.TryGetProperty("models", out var modelsElement) && modelsElement.ValueKind == JsonValueKind.Array)
            foreach (var item in modelsElement.EnumerateArray())
            {
                var name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var id = StripModelsPrefix(name);
                AddModel(models, id, LooksVisionCapable(id));
            }

        return models.OrderBy(static model => model.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static bool LooksVisionCapable(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;

        var id = modelId.ToLowerInvariant();

        if (id.Contains("embedding", StringComparison.Ordinal) ||
            id.Contains("embed-", StringComparison.Ordinal) ||
            id.Contains("whisper", StringComparison.Ordinal) ||
            id.Contains("tts", StringComparison.Ordinal) ||
            id.Contains("dall-e", StringComparison.Ordinal) ||
            id.Contains("imagen", StringComparison.Ordinal) ||
            id.Contains("veo", StringComparison.Ordinal) ||
            id.Contains("aqa", StringComparison.Ordinal) ||
            id.Contains("moderation", StringComparison.Ordinal) ||
            id.Contains("rerank", StringComparison.Ordinal) ||
            id.Contains("audio", StringComparison.Ordinal))
            return false;

        if (id.Contains("vision", StringComparison.Ordinal) ||
            id.Contains("llava", StringComparison.Ordinal) ||
            id.Contains("pixtral", StringComparison.Ordinal) ||
            id.Contains("moondream", StringComparison.Ordinal) ||
            id.Contains("minicpm-v", StringComparison.Ordinal) ||
            id.Contains("internvl", StringComparison.Ordinal) ||
            id.Contains("idefics", StringComparison.Ordinal) ||
            id.Contains("paligemma", StringComparison.Ordinal) ||
            id.Contains("florence", StringComparison.Ordinal) ||
            id.Contains("molmo", StringComparison.Ordinal) ||
            id.Contains("granite-vision", StringComparison.Ordinal) ||
            id.Contains("yi-vl", StringComparison.Ordinal) ||
            id.Contains("cogvlm", StringComparison.Ordinal) ||
            id.Contains("fuyu", StringComparison.Ordinal) ||
            id.Contains("kosmos", StringComparison.Ordinal) ||
            id.Contains("nvlm", StringComparison.Ordinal) ||
            id.Contains("smolvlm", StringComparison.Ordinal) ||
            id.Contains("gemma-3", StringComparison.Ordinal) ||
            id.Contains("gemma3", StringComparison.Ordinal) ||
            id.Contains("llama-4", StringComparison.Ordinal) ||
            id.Contains("llama4", StringComparison.Ordinal))
            return true;

        if (id.Contains("qwen", StringComparison.Ordinal) &&
            (id.Contains("vl", StringComparison.Ordinal) || id.Contains("omni", StringComparison.Ordinal)))
            return true;

        if (id.Contains("glm", StringComparison.Ordinal) &&
            (id.Contains("4v", StringComparison.Ordinal) ||
             id.Contains("5v", StringComparison.Ordinal) ||
             id.Contains("omni", StringComparison.Ordinal)))
            return true;

        if (id.Contains("gpt-4o", StringComparison.Ordinal) ||
            id.Contains("gpt-4.1", StringComparison.Ordinal) ||
            id.Contains("gpt-4.5", StringComparison.Ordinal) ||
            id.Contains("gpt-5", StringComparison.Ordinal) ||
            id.Contains("chatgpt-4o", StringComparison.Ordinal) ||
            id.Contains("claude", StringComparison.Ordinal) ||
            id.Contains("gemini", StringComparison.Ordinal))
            return true;

        if ((id.Contains("o3", StringComparison.Ordinal) || id.Contains("o4", StringComparison.Ordinal)) &&
            !id.Contains("pro", StringComparison.Ordinal))
            return true;

        if (id.Contains("llama-3.2", StringComparison.Ordinal) &&
            (id.Contains("11b", StringComparison.Ordinal) || id.Contains("90b", StringComparison.Ordinal)))
            return true;

        if (id.Contains("phi-3.5-vision", StringComparison.Ordinal) ||
            id.Contains("phi-4-multimodal", StringComparison.Ordinal) ||
            id.Contains("phi-4-vision", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool? ReadVisionFlag(JsonElement item)
    {
        if (!item.TryGetProperty("architecture", out var architecture) ||
            !architecture.TryGetProperty("input_modalities", out var modalities) ||
            modalities.ValueKind != JsonValueKind.Array)
            return null;

        var hasAny = false;
        var hasImage = false;

        foreach (var modality in modalities.EnumerateArray())
        {
            if (modality.ValueKind != JsonValueKind.String) continue;

            hasAny = true;
            if (string.Equals(modality.GetString(), "image", StringComparison.OrdinalIgnoreCase))
                hasImage = true;
        }

        return hasAny ? hasImage : null;
    }

    private static void AddModel(List<VisionModelInfo> models, string id, bool isVisionCapable)
    {
        var normalized = StripModelsPrefix(id.Trim());
        if (string.IsNullOrWhiteSpace(normalized)) return;

        if (models.Any(model => string.Equals(model.Id, normalized, StringComparison.OrdinalIgnoreCase))) return;

        models.Add(new VisionModelInfo(normalized, isVisionCapable));
    }

    private static string StripModelsPrefix(string id)
    {
        return id.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? id["models/".Length..]
            : id;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
