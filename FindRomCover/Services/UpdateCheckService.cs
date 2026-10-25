using System.Net;
using System.Reflection;
using System.Text.Json;
using FindRomCover.Models;

namespace FindRomCover.Services;

/// <summary>
///     Checks the GitHub releases API for a newer version of the application.
/// </summary>
public static class UpdateCheckService
{
    private const string GitHubReleasesUrl =
        "https://api.github.com/repos/purelogiccode/FindRomCover/releases/latest";

    private const string ReleasesPageUrl = "https://github.com/purelogiccode/FindRomCover/releases";

    /// <summary>
    ///     Overridable current version (used by tests). When null, the entry assembly
    ///     (the WPF or Avalonia head) version is used.
    /// </summary>
    internal static Version? CurrentVersionOverride { get; set; }

    /// <summary>
    ///     Gets the version of the running application.
    /// </summary>
    /// <returns>The entry assembly version, or <see langword="null" /> when it cannot be determined.</returns>
    private static Version? GetCurrentVersion()
    {
        return CurrentVersionOverride ??
               (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
    }

    /// <summary>
    ///     Checks for application updates via the GitHub API.
    /// </summary>
    /// <param name="httpClient">
    ///     An optional HttpClient to use. The caller retains ownership of the client's lifecycle
    ///     and is responsible for disposing it when no longer needed. If null, the shared
    ///     <see cref="HttpClientHelper.Client" /> is used.
    /// </param>
    public static async Task<UpdateInfo> CheckForUpdateAsync(HttpClient? httpClient = null)
    {
        var client = httpClient ?? HttpClientHelper.Client;

        try
        {
            var currentVersion = GetCurrentVersion();
            if (currentVersion == null)
            {
                LogService.WarningTransient("Could not determine current application version.");
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesUrl);
            request.Headers.Add("User-Agent", "FindRomCover");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await client.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    LogService.Debug($"GitHub API returned status code: {response.StatusCode} (likely rate-limited)");
                else
                    LogService.WarningTransient($"GitHub API returned status code: {response.StatusCode}");
                return new UpdateInfo { IsUpdateAvailable = false };
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);

            return ParseReleaseResponse(json, currentVersion);
        }
        catch (TaskCanceledException)
        {
            LogService.WarningTransient("Update check timed out.");
            return new UpdateInfo { IsUpdateAvailable = false };
        }
        catch (HttpRequestException ex)
        {
            LogService.WarningTransient(ex, "Update check failed (network error).");
            return new UpdateInfo { IsUpdateAvailable = false };
        }
        catch (ObjectDisposedException ex)
        {
            LogService.Debug($"Update check skipped during shutdown: {ex.Message}");
            return new UpdateInfo { IsUpdateAvailable = false };
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error checking for updates.");
            return new UpdateInfo { IsUpdateAvailable = false };
        }
    }

    /// <summary>
    ///     Parses a GitHub "latest release" JSON payload and compares it with the current version.
    /// </summary>
    /// <param name="json">The raw GitHub API response body.</param>
    /// <param name="currentVersion">The version currently running.</param>
    /// <returns>The parsed update information.</returns>
    internal static UpdateInfo ParseReleaseResponse(string json, Version currentVersion)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("tag_name", out var tagNameElement) ||
            tagNameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(tagNameElement.GetString()))
        {
            LogService.WarningTransient("GitHub release has no tag_name.");
            return new UpdateInfo { IsUpdateAvailable = false };
        }

        var tagName = tagNameElement.GetString()!;

        var latestVersion = ParseVersion(tagName);
        if (latestVersion == null)
        {
            LogService.WarningTransient($"Could not parse version from tag: {tagName}");
            return new UpdateInfo { IsUpdateAvailable = false };
        }

        var releaseUrl = GetStringOrEmpty(root, "html_url");
        if (string.IsNullOrEmpty(releaseUrl)) releaseUrl = ReleasesPageUrl;

        var releaseNotes = GetStringOrEmpty(root, "body");
        var publishedAt = GetStringOrEmpty(root, "published_at");

        var isUpdateAvailable = latestVersion > currentVersion;

        LogService.Information(isUpdateAvailable
            ? $"Update available: current={currentVersion}, latest={latestVersion}"
            : $"Application is up to date (v{currentVersion}).");

        return new UpdateInfo
        {
            IsUpdateAvailable = isUpdateAvailable,
            CurrentVersion = currentVersion.ToString(),
            LatestVersion = latestVersion.ToString(),
            ReleaseUrl = releaseUrl,
            ReleaseNotes = releaseNotes,
            PublishedAt = publishedAt
        };
    }

    /// <summary>
    ///     Reads a string property from a JSON object, returning an empty string when the
    ///     property is missing, null or not a string (GetString would otherwise throw).
    /// </summary>
    /// <param name="root">The JSON object to read from.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value, or an empty string.</returns>
    private static string GetStringOrEmpty(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    ///     Parses a release tag (with optional "v"/"release_" prefixes) into a version.
    /// </summary>
    /// <param name="tagName">The release tag name.</param>
    /// <returns>The parsed version, or <see langword="null" /> when the tag is not a version.</returns>
    internal static Version? ParseVersion(string tagName)
    {
        var versionString = tagName
            .TrimStart('v', 'V')
            .Replace("release_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("release-", "", StringComparison.OrdinalIgnoreCase)
            .TrimStart('v', 'V');

        if (Version.TryParse(versionString, out var version) ||
            Version.TryParse(versionString + ".0", out version))
            return version;

        return null;
    }
}
