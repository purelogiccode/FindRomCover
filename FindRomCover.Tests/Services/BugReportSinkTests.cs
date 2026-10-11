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

            return WaitForReportAsync(reports, report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
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

        reports.Should().Contain(report => string.Equals(report.Exception.Message, exception.Message, StringComparison.Ordinal));
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

            await Task.Delay(300);
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

            await Task.Delay(300);
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

            await Task.Delay(300);
        });

        reports.Should().NotContain(report => report.ErrorMessage.Contains(marker, StringComparison.Ordinal));
    }

    private static string CreateMarker()
    {
        return $"marker-{Guid.NewGuid():N}";
    }

    private static LogEvent CreateEvent(LogEventLevel level, string message, Exception? exception = null,
        LogEventProperty[]? properties = null)
    {
        var template = new MessageTemplateParser().Parse(message);
        return new LogEvent(DateTimeOffset.Now, level, exception, template, properties ?? []);
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
}
