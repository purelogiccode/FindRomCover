using System.Net;
using System.Text;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AnthropicVisionClientTests
{
    private static AiVisionOptions CreateOptions()
    {
        return new AiVisionOptions(
            true,
            AppConstants.AiProviders.Anthropic,
            "https://api.anthropic.com/v1",
            "test-key",
            "claude-sonnet-4-5",
            30,
            6,
            512,
            80,
            false,
            false,
            false);
    }

    private static List<VisionImageInput> CreateImages()
    {
        return [new VisionImageInput("a.png", new PreparedVisionImage("h1", [1, 2, 3]), 0)];
    }

    [Fact]
    public void BuildMessagesUrlShouldAppendPath()
    {
        AnthropicVisionClient.BuildMessagesUrl("https://api.anthropic.com/v1/")
            .Should().Be("https://api.anthropic.com/v1/messages");
    }

    [Fact]
    public void BuildRequestBodyShouldEmbedBase64Image()
    {
        var body = AnthropicVisionClient.BuildRequestBody(CreateOptions(), "system", "user", CreateImages());

        body.Should().Contain("\"type\":\"image\"");
        body.Should().Contain("base64");
        body.Should().Contain("claude-sonnet-4-5");
    }

    [Fact]
    public void ExtractMessageContentShouldJoinTextBlocks()
    {
        const string json =
            "{\"content\":[{\"type\":\"text\",\"text\":\"a\"},{\"type\":\"tool_use\"},{\"type\":\"text\",\"text\":\"b\"}]}";

        AnthropicVisionClient.ExtractMessageContent(json).Should().Be("ab");
    }

    [Fact]
    public void ExtractMessageContentShouldThrowWhenContentIsMissing()
    {
        var act = () => AnthropicVisionClient.ExtractMessageContent("{}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task PickBestAsyncShouldSendAnthropicHeadersAndParseResponse()
    {
        string? apiKey = null;
        string? version = null;
        string? path = null;

        using var handler = new StubHttpMessageHandler(request =>
        {
            request.Headers.TryGetValues("x-api-key", out var keyValues);
            apiKey = keyValues?.FirstOrDefault();
            request.Headers.TryGetValues("anthropic-version", out var versionValues);
            version = versionValues?.FirstOrDefault();
            path = request.RequestUri?.AbsolutePath;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"bestIndex\\\":0,\\\"confidence\\\":0.9,\\\"reason\\\":\\\"ok\\\"}\"}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        using var httpClient = new HttpClient(handler);
        using var client = new AnthropicVisionClient(httpClient);

        var result = await client.PickBestAsync(CreateOptions(), "Game", "Game", CreateImages(), CancellationToken.None);

        result.BestIndex.Should().Be(0);
        result.Confidence.Should().BeApproximately(0.9, 0.001);
        apiKey.Should().Be("test-key");
        version.Should().Be("2023-06-01");
        path.Should().Be("/v1/messages");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
