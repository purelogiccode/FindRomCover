using System.Text.Json;
using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ErrorLoggerRetryPendingReportsTests : IDisposable
{
    private static readonly string PendingReportsPath =
        Path.Combine(AppDataPaths.BaseDirectory, "PendingBugReports.json");

    public ErrorLoggerRetryPendingReportsTests()
    {
        using var scope = ErrorLogger.DisposeLock.EnterScope();
        ErrorLogger.IsDisposed = false;

        AppDataPaths.EnsureBaseDirectory();
        if (File.Exists(PendingReportsPath)) File.Delete(PendingReportsPath);
    }

    public void Dispose()
    {
        using (ErrorLogger.DisposeLock.EnterScope())
        {
            ErrorLogger.IsDisposed = false;
        }

        if (File.Exists(PendingReportsPath)) File.Delete(PendingReportsPath);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RetryShouldDropStaleTestHostReportsWithoutCallingTheApi()
    {
        // Regression test: a queue left behind by an older test run contained fake
        // "test"/marker reports. Retrying it must remove those entries without any API
        // call, even when the network would be available and an earlier real report
        // fails transiently.
        var marker = $"stale-testhost-{Guid.NewGuid():N}";
        var queue = new[]
        {
            new
            {
                id = Guid.NewGuid().ToString("N"),
                report = new
                {
                    applicationName = "FindRomCover",
                    baseDirectory = @"C:\repos\FindRomCover.Tests\bin\Release\net10.0-windows",
                    errorMessage = marker,
                    exception = new
                    {
                        type = "System.InvalidOperationException",
                        message = marker
                    }
                }
            }
        };

        await File.WriteAllTextAsync(PendingReportsPath, JsonSerializer.Serialize(queue));

        var sends = 0;
        var originalOverride = ErrorLogger.SendToApiOverride;

        try
        {
            // Simulate an offline API: before the fix, a transient failure on an earlier
            // entry left the fake reports in the queue for the next launch.
            ErrorLogger.SendToApiOverride = (_, _) =>
            {
                Interlocked.Increment(ref sends);
                return Task.FromResult(false);
            };

            await ErrorLogger.RetryPendingReportsAsync();

            sends.Should().Be(0, "a stale test-host report must never reach the API");

            var remainingQueue = File.Exists(PendingReportsPath)
                ? await File.ReadAllTextAsync(PendingReportsPath)
                : string.Empty;
            remainingQueue.Should().NotContain(marker);
        }
        finally
        {
            ErrorLogger.SendToApiOverride = originalOverride;
        }
    }

    [Fact]
    public async Task RetryShouldDropMalformedEntriesWithoutThrowing()
    {
        // Regression test: a hand-edited or truncated queue can contain "report": null.
        // The retry used to throw from the pre-filter before the queue was rewritten.
        var marker = $"malformed-{Guid.NewGuid():N}";
        var malformedId = Guid.NewGuid().ToString("N");
        object[] queue =
        [
            new
            {
                id = malformedId,
                report = (object?)null
            },
            new
            {
                id = Guid.NewGuid().ToString("N"),
                report = new
                {
                    applicationName = "FindRomCover",
                    baseDirectory = @"C:\Program Files\FindRomCover",
                    errorMessage = marker,
                    exception = new
                    {
                        type = "System.InvalidOperationException",
                        message = marker
                    }
                }
            }
        ];

        await File.WriteAllTextAsync(PendingReportsPath, JsonSerializer.Serialize(queue));

        var originalOverride = ErrorLogger.SendToApiOverride;

        try
        {
            ErrorLogger.SendToApiOverride = static (_, _) => Task.FromResult(false);

            var act = ErrorLogger.RetryPendingReportsAsync;

            await act.Should().NotThrowAsync();

            var remainingQueue = File.Exists(PendingReportsPath)
                ? await File.ReadAllTextAsync(PendingReportsPath)
                : string.Empty;
            remainingQueue.Should().Contain(marker, "a valid entry must stay queued for the next launch");
            remainingQueue.Should().NotContain(malformedId, "the malformed entry must be dropped");
        }
        finally
        {
            ErrorLogger.SendToApiOverride = originalOverride;
        }
    }
}
