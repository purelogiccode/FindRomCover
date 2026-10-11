using FindRomCover.Services;
using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace FindRomCover.Tests.Services;

public class MessageTextTemplateFormatterTests
{
    private readonly MessageTextTemplateFormatter _formatter = new();

    [Fact]
    public void FormatShouldWriteTimestampLevelAndMessage()
    {
        var logEvent = CreateEvent(LogEventLevel.Warning, "something happened", null);

        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);

        var text = writer.ToString();
        text.Should().Contain(" [WARNING] something happened");
        text.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[WARNING\] something happened");
    }

    [Fact]
    public void FormatShouldAppendExceptionDetails()
    {
        var exception = new InvalidOperationException("boom");
        var logEvent = CreateEvent(LogEventLevel.Error, "failed", exception);

        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);

        var text = writer.ToString();
        text.Should().Contain("[ERROR] failed");
        text.Should().Contain("InvalidOperationException");
        text.Should().Contain("boom");
    }

    [Fact]
    public void FormatShouldNotWriteExceptionBlockWhenNoException()
    {
        var logEvent = CreateEvent(LogEventLevel.Information, "ok", null);

        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);

        writer.ToString().Should().NotContain("Exception");
    }

    private static LogEvent CreateEvent(LogEventLevel level, string message, Exception? exception)
    {
        return new LogEvent(DateTimeOffset.Now, level, exception, new MessageTemplateParser().Parse(message), []);
    }
}