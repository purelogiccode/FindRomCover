using System.Collections.Concurrent;
using FindRomCover.Models;
using FindRomCover.Services;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace FindRomCover.Tests.Services;

public class BugReportSinkTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public BugReportSinkTests()
    {
        using var scope = ErrorLogger.DisposeLock.EnterScope();
        ErrorLogger.IsDisposed = false;
    }

    public void Dispose()
    {
        using (ErrorLogger.DisposeLock.EnterScope())
        {
            ErrorLogger.IsDisposed = false;
        }

        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, false)]
    [InlineData(LogEventLevel.Debug, false)]
    [InlineData(LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, true)]
    [InlineData(LogEventLevel.Error, true)]
    [InlineData(LogEventLevel.Fatal, true)]
    public void IsForwardableShouldForwardWarningsAndAbove(LogEventLevel level, bool expected)
    {
        BugReportSink.IsForwardable(level).Should().Be(expected);
    }

    [Fact]
    public async Task EmitShouldForwardWarningToBugReportApi()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();

        await WithCapturedReports(reports, () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());
            sink.Emit(CreateEvent(LogEventLevel.Warning, marker));

            return WaitForReportAsync(reports,
                report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
        });

        reports.Should().Contain(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitShouldForwardErrorWithExceptionToBugReportApi()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();
        var exception = new InvalidOperationException($"boom-{marker}");

        await WithCapturedReports(reports, () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());
            sink.Emit(CreateEvent(LogEventLevel.Error, marker, exception));

            return WaitForReportAsync(reports,
                report => string.Equals(report.Exception.Message, exception.Message, StringComparison.Ordinal));
        });

        reports.Should().Contain(report =>
            string.Equals(report.Exception.Message, exception.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitShouldNotForwardInformationEvents()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();

        await WithCapturedReports(reports, async () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());
            sink.Emit(CreateEvent(LogEventLevel.Information, marker));

            await WaitForForwardingToSettleAsync();
        });

        reports.Should().NotContain(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitShouldSuppressTransientEnvironmentWarnings()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();

        await WithCapturedReports(reports, async () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());
            sink.Emit(CreateEvent(LogEventLevel.Warning, marker,
                properties: [new LogEventProperty(LogService.TransientErrorPropertyName, new ScalarValue(true))]));

            await WaitForForwardingToSettleAsync();
        });

        reports.Should().NotContain(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitShouldSuppressWatcherMarkedErrors()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();

        await WithCapturedReports(reports, async () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());
            sink.Emit(CreateEvent(LogEventLevel.Error, marker,
                exception: new IOException("locked file"),
                properties: [new LogEventProperty(LogService.WatcherErrorPropertyName, new ScalarValue(true))]));

            await WaitForForwardingToSettleAsync();
        });

        reports.Should().NotContain(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmitShouldThrottleRepeatedEventsWithTheSameTemplate()
    {
        var marker = CreateMarker();
        var reports = new ConcurrentBag<BugReportModel>();
        var exception = new InvalidOperationException("same failure");

        await WithCapturedReports(reports, async () =>
        {
            var sink = new BugReportSink(new MessageTextTemplateFormatter());

            sink.Emit(CreateEvent(LogEventLevel.Error, marker, exception, timestamp: DateTimeOffset.Now));
            await WaitForReportAsync(reports, report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));

            // The rendered message embeds the timestamp, so the old throttle key changed
            // on every event and filed a separate report for each repetition.
            sink.Emit(CreateEvent(LogEventLevel.Error, marker, exception,
                timestamp: DateTimeOffset.Now.AddSeconds(5)));
            await WaitForForwardingToSettleAsync();
        });

        reports.Count(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal)).Should().Be(1);
    }

    private static string CreateMarker()
    {
        return $"marker-{Guid.NewGuid():N}";
    }

    private static LogEvent CreateEvent(LogEventLevel level, string message, Exception? exception = null,
        LogEventProperty[]? properties = null, DateTimeOffset? timestamp = null)
    {
        var template = new MessageTemplateParser().Parse(message);
        return new LogEvent(timestamp ?? DateTimeOffset.Now, level, exception, template, properties ?? []);
    }

    private static Task WithCapturedReports(ConcurrentBag<BugReportModel> reports, Func<Task> action)
    {
        var original = ErrorLogger.SendToApiOverride;

        using (ErrorLogger.DisposeLock.EnterScope())
        {
            ErrorLogger.IsDisposed = false;
        }

        ErrorLogger.SendToApiOverride = (report, _) =>
        {
            reports.Add(report);
            return Task.FromResult(false);
        };

        return RunAndRestoreAsync();

        async Task RunAndRestoreAsync()
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                ErrorLogger.SendToApiOverride = original;
            }
        }
    }

    private static async Task WaitForReportAsync(ConcurrentBag<BugReportModel> reports,
        Func<BugReportModel, bool> predicate)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (reports.Any(predicate)) return;
            await Task.Delay(25);
        }
    }

    /// <summary>
    ///     Waits until the sink's fire-and-forget forwarding has actually started and
    ///     completed, so "not forwarded" assertions are deterministic instead of relying
    ///     on a fixed sleep.
    /// </summary>
    private static async Task WaitForForwardingToSettleAsync()
    {
        // Give the fire-and-forget LogAsync call a chance to start.
        await Task.Delay(50);

        var deadline = DateTime.UtcNow + WaitTimeout;
        while (DateTime.UtcNow < deadline && ErrorLogger.PendingOperations > 0)
            await Task.Delay(10);

        // A second quiet period covers the gap between log-file write and API send.
        await Task.Delay(50);
    }
}