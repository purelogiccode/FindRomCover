using System.Net;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiProviderExceptionTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void IsTransientShouldOnlyAcceptTemporaryFailures(HttpStatusCode statusCode, bool expected)
    {
        var exception = new AiProviderException(statusCode, "provider failed");

        exception.IsTransient.Should().Be(expected);
        exception.StatusCode.Should().Be((int)statusCode);
    }

    [Fact]
    public void ShouldRemainAnInvalidOperationException()
    {
        var exception = new AiProviderException(HttpStatusCode.BadGateway, "provider failed");

        exception.Should().BeAssignableTo<InvalidOperationException>();
        exception.Message.Should().Be("provider failed");
    }
}
