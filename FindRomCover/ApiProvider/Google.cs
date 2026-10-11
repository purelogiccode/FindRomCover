using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Web;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;

namespace FindRomCover.ApiProvider;

/// <summary>
///     Google Custom Search image provider used to fetch candidate cover images.
/// </summary>
public static class Google
{
    private const int MaxResults = 10;
    private const string ProviderName = "Google";
    private const string SearchEngineId = "d30e97188f5914611";

    private static readonly JsonSerializerOptions LogJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    ///     Builds the Google Custom Search image request URL for the given query.
    /// </summary>
    /// <param name="searchQuery">The text to search for.</param>
    /// <param name="settingsManager">The settings manager that supplies the Google API key.</param>
    /// <returns>The fully encoded request URL.</returns>
    internal static string BuildRequestUrl(string searchQuery, SettingsManager settingsManager)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchQuery);
        var encodedSearchQuery = HttpUtility.UrlEncode(searchQuery.Trim());

        if (string.IsNullOrEmpty(settingsManager.GoogleKey))
            throw new InvalidOperationException("Google API Key is not configured");

        LogService.Debug($"Google Search Query: {searchQuery}");
        return
            $"https://www.googleapis.com/customsearch/v1?q={encodedSearchQuery}&cx={HttpUtility.UrlEncode(SearchEngineId)}&num={MaxResults}&searchType=image&key={HttpUtility.UrlEncode(settingsManager.GoogleKey)}";
    }

    /// <summary>
    ///     Deserializes a Google Custom Search JSON response.
    /// </summary>
    /// <param name="json">The JSON response body.</param>
    /// <param name="jsonOptions">The serializer options to use.</param>
    /// <returns>The deserialized <see cref="GoogleSearchResult" />, or <c>null</c> when the payload is empty.</returns>
    internal static GoogleSearchResult? DeserializeResponse(string json, JsonSerializerOptions jsonOptions)
    {
        return JsonSerializer.Deserialize<GoogleSearchResult>(json, jsonOptions);
    }

    /// <summary>
    ///     Maps Google search result items to image data entries.
    /// </summary>
    /// <param name="searchResults">The search results to map; may be <c>null</c>.</param>
    /// <returns>The mapped list of <see cref="ImageData" /> entries; empty when there are no results.</returns>
    internal static List<ImageData> MapToImageData(GoogleSearchResult? searchResults)
    {
        if (searchResults?.Items != null)
            return searchResults.Items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Link))
                .Select(static item => new ImageData
                {
                    ImagePath = item.Link,
                    ThumbnailUrl = item.Image?.ThumbnailLink,
                    ImageName = FormatImageName(item.Title),
                    ImageFileSize = item.Image is { ByteSize: > 0 }
                        ? Math.Round(item.Image.ByteSize / 1024.0, 2).ToString(CultureInfo.InvariantCulture) + " KB"
                        : "Unknown",
                    ImageEncodingFormat = item.Mime,
                    ImageWidth = item.Image is { Width: > 0 } ? item.Image.Width : 1,
                    ImageHeight = item.Image is { Height: > 0 } ? item.Image.Height : 1,
                    ThumbnailWidth = 0,
                    ThumbnailHeight = 0
                })
                .ToList();

        return new List<ImageData>();
    }

    /// <summary>
    ///     Fetches candidate cover images from the Google Custom Search API.
    /// </summary>
    /// <param name="searchQuery">The text to search for.</param>
    /// <param name="settingsManager">The settings manager that supplies the API key and timeout.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>A task whose result is the list of found images.</returns>
    public static async Task<List<ImageData>> FetchImagesFromGoogleAsync(string searchQuery,
        SettingsManager settingsManager, CancellationToken cancellationToken = default)
    {
        var requestUrl = BuildRequestUrl(searchQuery, settingsManager);

        const string logMessagePrefix = $"{ProviderName} API";
        var maskedUrl = MaskApiKey(requestUrl);
        LogService.Debug($"{logMessagePrefix} Request: GET {maskedUrl}");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(settingsManager.ApiTimeoutSeconds));

        try
        {
            // GetAsync must stay inside the try: with the default ResponseContentRead
            // option it buffers the body, so timeouts and connection failures surface
            // here and must be mapped to the friendly messages below.
            using var response = await HttpClientHelper.Client.GetAsync(requestUrl, cts.Token);

            LogService.Debug($"{logMessagePrefix} Response Status: {response.StatusCode}");

            switch (response.StatusCode)
            {
                case HttpStatusCode.TooManyRequests:
                {
                    LogService.Warning($"{logMessagePrefix} API rate limit exceeded (429).");
                    var rateLimitException = new HttpRequestException(
                        "Response status code does not indicate success: 429 (Too Many Requests).", null,
                        HttpStatusCode.TooManyRequests);
                    throw new InvalidOperationException(
                        $"{ProviderName} API rate limit has been exceeded. Please wait a moment before trying again.",
                        rateLimitException);
                }
                case HttpStatusCode.Forbidden:
                {
                    LogService.Warning(
                        $"{logMessagePrefix} Access Forbidden (403). Check API Key and API Permissions.");
                    var forbiddenEx = new HttpRequestException("403 (Forbidden)", null, HttpStatusCode.Forbidden);

                    throw new InvalidOperationException(
                        $"{ProviderName} API Access Forbidden (403).\n\n" +
                        "This usually means:\n" +
                        "1. Your API Key is incorrect.\n" +
                        "2. Your daily free limit has been reached.", forbiddenEx);
                }
                case HttpStatusCode.Unauthorized:
                {
                    LogService.Warning($"{logMessagePrefix} Unauthorized (401). The API key was rejected.");
                    var unauthorizedEx = new HttpRequestException(
                        "Response status code does not indicate success: 401 (Unauthorized).", null,
                        HttpStatusCode.Unauthorized);
                    throw new InvalidOperationException(
                        $"{ProviderName} API rejected the API key (401 Unauthorized). " +
                        "Please check your Google API Key in Settings > API Settings.", unauthorizedEx);
                }
                case HttpStatusCode.BadRequest:
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogService.Warning($"{logMessagePrefix} Bad Request (400): {errorBody}");
                    var badRequestEx = new HttpRequestException(
                        "Response status code does not indicate success: 400 (Bad Request).", null,
                        HttpStatusCode.BadRequest);
                    var friendlyReason = ExtractGoogleErrorMessage(errorBody);
                    throw new InvalidOperationException(
                        $"{ProviderName} API error: {friendlyReason}. " +
                        "Please check your Google API Key and Search Engine ID in Settings > API Settings.",
                        badRequestEx);
                }
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            LogService.Debug($"{logMessagePrefix} Response Body received.");

            response.EnsureSuccessStatusCode();

            var deserializedResponse = DeserializeResponse(json, LogJsonSerializerOptions);
            var imageDataList = MapToImageData(deserializedResponse);

            LogService.Information($"{logMessagePrefix} Successfully parsed {imageDataList.Count} images.");
            return imageDataList;
        }
        catch (HttpRequestException ex)
        {
            LogService.Error(ex, $"{logMessagePrefix} HTTP Error: {ex.StatusCode} - {ex.Message}");
            throw new InvalidOperationException(
                $"{ProviderName} API error: {ex.Message}. Please check your API key or internet connection.", ex);
        }
        catch (JsonException ex)
        {
            LogService.Error(ex, $"Failed to deserialize {ProviderName} search results");
            throw new InvalidOperationException(
                $"Failed to parse {ProviderName} API response. The service might be experiencing issues.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Either the linked CTS (ApiTimeoutSeconds) fired first, or HttpClient's own
            // internal timeout fired. In both cases the outer caller's token is NOT
            // cancelled, so this is a timeout — map it to the friendly message instead
            // of letting the raw TaskCanceledException escape.
            LogService.Error(ex, $"{ProviderName} API request timed out");
            throw new InvalidOperationException($"{ProviderName} API request timed out. Please try again.", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogService.Information($"{logMessagePrefix} request was cancelled.");
            throw; // Re-throw cancellation
        }
    }

    /// <summary>
    ///     Extracts a human-readable error message from a Google API error response body.
    /// </summary>
    /// <param name="errorBody">The raw error response body.</param>
    /// <returns>The error message, or a truncated copy of the body when it cannot be parsed.</returns>
    internal static string ExtractGoogleErrorMessage(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody)) return "Invalid request.";

        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch (JsonException ex)
        {
            // Response body was not JSON; fall through to the raw body.
            LogService.Debug($"Google API error body was not JSON: {ex.Message}");
        }

        return errorBody.Length > 300 ? errorBody[..300] + "..." : errorBody;
    }

    private static string MaskApiKey(string url)
    {
        const string keyParam = "&key=";
        var keyIndex = url.IndexOf(keyParam, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return url;

        var keyStart = keyIndex + keyParam.Length;
        var keyEnd = url.IndexOf('&', keyStart);
        if (keyEnd < 0) keyEnd = url.Length;

        // Never emit any characters of the API key into logs, not even partial ones.
        return url[..keyStart] + "****" + url[keyEnd..];
    }

    /// <summary>
    ///     Formats an image title or URL into a display-friendly, title-cased name.
    /// </summary>
    /// <param name="input">The title text or image URL to format.</param>
    /// <returns>The formatted image name; empty when the input is blank.</returns>
    internal static string FormatImageName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
            try
            {
                var uri = new Uri(input);
                var fileName = Path.GetFileNameWithoutExtension(uri.LocalPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    var textInfo = CultureInfo.InvariantCulture.TextInfo;
                    return textInfo.ToTitleCase(fileName.ToLower(CultureInfo.InvariantCulture)
                        .Replace("-", " ")
                        .Replace("_", " "));
                }
            }
            catch (Exception ex)
            {
                // Fall back to title formatting if URL parsing fails
                LogService.Debug($"Could not derive image name from URL '{input}': {ex.Message}");
            }

        var textInfoTitle = CultureInfo.InvariantCulture.TextInfo;
        return textInfoTitle.ToTitleCase(input.ToLower(CultureInfo.InvariantCulture)
            .Replace("-", " ")
            .Replace("_", " "));
    }
}