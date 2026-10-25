using System.Net;
using System.Text;
using System.Text.Json;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class GeminiVisionClientTests
{
    private static AiVisionOptions CreateOptions()
    {
        return new AiVisionOptions(
            true,
            AppConstants.AiProviders.Gemini,
            "https://generativelanguage.googleapis.com/v1beta",
            "test-key",
            "gemini-2.5-flash",
            30,
            6,
            512,
            80,
            false,
            false,
            false,
            70);
    }

    private static List<VisionImageInput> CreateImages()
    {
        return [new VisionImageInput("a.png", new PreparedVisionImage("h1", [1, 2, 3]), 0)];
    }

    [Fact]
    public void BuildGenerateContentUrlShouldStripModelsPrefix()
    {
        GeminiVisionClient.BuildGenerateContentUrl(
                "https://generativelanguage.googleapis.com/v1beta/", "models/gemini-2.5-flash")
            .Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
    }

    [Fact]
    public void BuildRequestBodyShouldEmbedInlineImageData()
    {
        var body = GeminiVisionClient.BuildRequestBody(CreateOptions(), "system", "user", CreateImages());

        body.Should().Contain("inlineData");
        body.Should().Contain("mimeType");
        body.Should().Contain("systemInstruction");
    }

    [Fact]
    public void BuildRequestBodyShouldApplyCustomRequestParameters()
    {
        var options = CreateOptions() with
        {
            RequestParametersJson = "{\"generationConfig\":{\"temperature\":0.4,\"topP\":0.9}}"
        };

        var body = GeminiVisionClient.BuildRequestBody(options, "system", "user", CreateImages());

        using var document = JsonDocument.Parse(body);
        var generationConfig = document.RootElement.GetProperty("generationConfig");
        generationConfig.GetProperty("temperature").GetDouble().Should().Be(0.4);
        generationConfig.GetProperty("topP").GetDouble().Should().Be(0.9);
        generationConfig.GetProperty("maxOutputTokens").GetInt32().Should().Be(4096);
    }

    [Fact]
    public void ExtractMessageContentShouldJoinTextParts()
    {
        const string json =
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"a\"},{\"text\":\"b\"}]}}]}";

        GeminiVisionClient.ExtractMessageContent(json).Should().Be("ab");
    }

    [Fact]
    public void ExtractMessageContentShouldThrowWhenBlocked()
    {
        var act = () => GeminiVisionClient.ExtractMessageContent("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*SAFETY*");
    }

    [Fact]
    public void ExtractMessageContentShouldExplainExhaustedOutputBudget()
    {
        const string json = "{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\",\"content\":{\"parts\":[]}}]}";

        var act = () => GeminiVisionClient.ExtractMessageContent(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*output tokens*");
    }

    [Fact]
    public async Task PickBestAsyncShouldSendApiKeyHeaderAndParseResponse()
    {
        string? apiKey = null;
        string? path = null;

        using var handler = new StubHttpMessageHandler(request =>
        {
            request.Headers.TryGetValues("x-goog-api-key", out var keyValues);
            apiKey = keyValues?.FirstOrDefault();
            path = request.RequestUri?.AbsolutePath;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"bestIndex\\\":0,\\\"confidence\\\":0.85,\\\"reason\\\":\\\"ok\\\"}\"}]}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        using var httpClient = new HttpClient(handler);
        using var client = new GeminiVisionClient(httpClient);

        var result =
            await client.PickBestAsync(CreateOptions(), "Game", "Game", CreateImages(), CancellationToken.None);

        result.BestIndex.Should().Be(0);
        result.Confidence.Should().BeApproximately(0.85, 0.001);
        apiKey.Should().Be("test-key");
        path.Should().Be("/v1beta/models/gemini-2.5-flash:generateContent");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
