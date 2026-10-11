using FindRomCover.Models;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Models;

public class MessageEnumsTests
{
    [Fact]
    public void MessageIconShouldExposeTheExpectedValuesInOrder()
    {
        Enum.GetValues<MessageIcon>().Should().Equal(
            MessageIcon.None,
            MessageIcon.Information,
            MessageIcon.Warning,
            MessageIcon.Error,
            MessageIcon.Question);
    }

    [Fact]
    public void MessageResultShouldExposeTheExpectedValuesInOrder()
    {
        Enum.GetValues<MessageResult>().Should().Equal(
            MessageResult.None,
            MessageResult.Ok,
            MessageResult.Yes,
            MessageResult.No,
            MessageResult.Cancel);
    }

    [Fact]
    public void ImageSaveResultShouldDefaultToSuccessWithoutErrorDetails()
    {
        var result = new ImageSaveResult(true);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorTitle.Should().BeNull();
        result.ErrorIcon.Should().Be(MessageIcon.None);
        result.Exception.Should().BeNull();
        result.LogContext.Should().BeNull();
    }

    [Fact]
    public void ImageSaveResultShouldCarryFailureDetails()
    {
        var exception = new IOException("locked");
        var result = new ImageSaveResult(false, "message", "title", MessageIcon.Error, exception, "context");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("message");
        result.ErrorTitle.Should().Be("title");
        result.ErrorIcon.Should().Be(MessageIcon.Error);
        result.Exception.Should().BeSameAs(exception);
        result.LogContext.Should().Be("context");
    }
}
