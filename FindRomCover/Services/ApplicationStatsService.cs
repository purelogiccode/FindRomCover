using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FindRomCover.Services;

/// <summary>
///     Sends an anonymous usage/telemetry hit to the PureLogicCode ApplicationStats
///     endpoint once per application launch.
/// </summary>
public static class ApplicationStatsService
{
    private const string StatsApiUrl = "https://www.purelogiccode.com/ApplicationStats/stats";
    private const string ApiKey = AppConstants.BugReportApiKey;
    private const string ApplicationId = "findromcover";

    /// <summary>
    ///     Records a start-up hit for this application. Failures are expected when the
    ///     machine is offline or the endpoint is rate-limited and are therefore marked
    ///     as transient (not forwarded to the bug report API).
    /// </summary>
    /// <returns>A task that completes when the attempt has finished.</returns>
    public static async Task RecordStartupAsync()
    {
        try
        {
            var version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ??
                          "Unknown";

            var payload = new
            {
                applicationId = ApplicationId,
                version
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, StatsApiUrl);

            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {ApiKey}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await HttpClientHelper.Client.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
                LogService.Information("Application stats recorded successfully.");
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                LogService.Debug("Application stats API rate-limited (429).");
            else
                LogService.WarningTransient($"Application stats API returned: {response.StatusCode}");
        }
        catch (TaskCanceledException ex)
        {
            LogService.WarningTransient(ex, "Application stats request timed out.");
        }
        catch (HttpRequestException ex)
        {
            LogService.WarningTransient(ex, "Failed to record application stats (network error).");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to record application stats.");
        }
    }
}
