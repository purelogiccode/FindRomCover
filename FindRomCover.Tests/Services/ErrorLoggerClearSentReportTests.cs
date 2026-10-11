using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ErrorLoggerClearSentReportTests
{
    [Fact]
    public async Task SuccessfulSendShouldOnlyRemoveTheReportThatWasAccepted()
    {
        using (ErrorLogger.DisposeLock.EnterScope())
        {
            ErrorLogger.IsDisposed = false;
        }

        var originalOverride = ErrorLogger.SendToApiOverride;
        var markerA = $"pending-{Guid.NewGuid():N}";
        var markerB = $"accepted-{Guid.NewGuid():N}";

        try
        {
            // Report A fails to send (stays pending).
            ErrorLogger.SendToApiOverride = static (_, _) => Task.FromResult(false);
            await ErrorLogger.LogAsync(new InvalidOperationException(markerA), markerA);

            // Report B is accepted by the API.
            ErrorLogger.SendToApiOverride = (report, _) =>
                Task.FromResult(report.ErrorMessage.Contains(markerB, StringComparison.Ordinal));
            await ErrorLogger.LogAsync(new InvalidOperationException(markerB), markerB);

            var pending = await File.ReadAllTextAsync(ErrorLogger.ApiLogFilePath);
            pending.Should().Contain(markerA, "a failed send must stay queued");
            pending.Should().NotContain(markerB, "the accepted report must be dequeued");
        }
        finally
        {
            ErrorLogger.SendToApiOverride = originalOverride;
        }
    }
}