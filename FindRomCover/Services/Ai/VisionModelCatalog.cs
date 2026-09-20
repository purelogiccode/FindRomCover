using System.Net.Http;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services.Ai;

internal static class VisionModelCatalog
{
    public static async Task<List<string>> FetchModelIdsAsync(
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

            return ParseModelIds(body);
        }
    }

    internal static string BuildModelsUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL is not configured.");

        return baseUrl.Trim().TrimEnd('/') + "/models";
    }

    internal static List<string> ParseModelIds(string json)
    {
        var ids = new List<string>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    AddId(ids, id.GetString());

        if (root.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            foreach (var item in models.EnumerateArray())
                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    AddId(ids, name.GetString());

        return ids.OrderBy(static id => id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddId(List<string> ids, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var normalized = id.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? id["models/".Length..]
            : id;

        if (!string.IsNullOrWhiteSpace(normalized) &&
            !ids.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            ids.Add(normalized);
    }
}
