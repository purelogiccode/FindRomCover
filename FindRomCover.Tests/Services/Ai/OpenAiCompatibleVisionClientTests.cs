using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class OpenAiCompatibleVisionClientTests
{
    private static AiVisionOptions CreateOptions()
    {
        return new AiVisionOptions(
            true,
            "OpenRouter",
            "https://example.test/api/v1",
            "test-key",
            "test-model",
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
        return
        [
            new VisionImageInput("a.png", new PreparedVisionImage("h1", [1]), 0),
            new VisionImageInput("b.png", new PreparedVisionImage("h2", [2]), 1)
        ];
    }

    [Fact]
    public void ParsePickResponseShouldParseJsonObject()
    {
        var result = VisionModelClientBase.ParsePickResponse(
            "{\"bestIndex\":2,\"confidence\":0.87,\"reason\":\"box art\"}");

        result.BestIndex.Should().Be(2);
        result.Confidence.Should().BeApproximately(0.87, 0.001);
        result.Reason.Should().Be("box art");
        result.HasPick.Should().BeTrue();
    }

    [Fact]
    public void ParsePickResponseShouldHandleFencedJsonAndStringValues()
    {
        const string content = "```json\n{\"bestIndex\":\"1\",\"confidence\":\"85\",\"reason\":\"ok\"}\n```";

        var result = VisionModelClientBase.ParsePickResponse(content);

        result.BestIndex.Should().Be(1);
        result.Confidence.Should().BeApproximately(0.85, 0.001);
    }

    [Fact]
    public void ParsePickResponseShouldTreatNegativeIndexAsNoPick()
    {
        var result = VisionModelClientBase.ParsePickResponse(
            "{\"bestIndex\":-1,\"confidence\":0.2,\"reason\":\"none\"}");

        result.HasPick.Should().BeFalse();
    }

    [Fact]
    public void ParsePickResponseShouldThrowWhenNoJsonObjectIsPresent()
    {
        var act = () => VisionModelClientBase.ParsePickResponse("no json here");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseVerificationResponseShouldParseBooleanMatch()
    {
        var result = VisionModelClientBase.ParseVerificationResponse(
            "{\"match\":true,\"confidence\":0.9,\"reason\":\"yes\"}");

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeApproximately(0.9, 0.001);
    }

    [Fact]
    public void ParseVerificationResponseShouldParseYesNoStrings()
    {
        VisionModelClientBase.ParseVerificationResponse("{\"match\":\"yes\",\"confidence\":0.5}")
            .IsMatch.Should().BeTrue();
        VisionModelClientBase.ParseVerificationResponse("{\"match\":\"no\",\"confidence\":0.5}")
            .IsMatch.Should().BeFalse();
    }

    [Fact]
    public void ExtractMessageContentShouldHandleStringContent()
    {
        const string json = "{\"choices\":[{\"message\":{\"content\":\"hello\"}}]}";

        OpenAiCompatibleVisionClient.ExtractMessageContent(json).Should().Be("hello");
    }

    [Fact]
    public void ExtractMessageContentShouldHandleArrayContent()
    {
        const string json =
            "{\"choices\":[{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"a\"},{\"type\":\"text\",\"text\":\"b\"}]}}]}";

        OpenAiCompatibleVisionClient.ExtractMessageContent(json).Should().Be("ab");
    }

    [Fact]
    public void ExtractMessageContentShouldThrowWhenNoChoices()
    {
        var act = () => OpenAiCompatibleVisionClient.ExtractMessageContent("{\"choices\":[]}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExtractMessageContentShouldExplainExhaustedOutputBudget()
    {
        const string json =
            "{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":null,\"reasoning\":\"thinking\"}}]}";

        var act = () => OpenAiCompatibleVisionClient.ExtractMessageContent(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*output tokens*");
    }

    [Fact]
    public void BuildRequestBodyShouldReserveRoomForReasoningModels()
    {
        var body = OpenAiCompatibleVisionClient.BuildRequestBody(
            CreateOptions(),
            "system",
            "user",
            CreateImages());

        body.Should().Contain("4096");
    }

    [Fact]
    public void BuildChatCompletionsUrlShouldAppendPath()
    {
        OpenAiCompatibleVisionClient.BuildChatCompletionsUrl("https://example.test/api/v1/")
            .Should().Be("https://example.test/api/v1/chat/completions");
    }

    [Fact]
    public void BuildRequestBodyShouldEmbedBase64ImageData()
    {
        var body = OpenAiCompatibleVisionClient.BuildRequestBody(
            CreateOptions(),
            "system",
            "user",
            CreateImages());

        body.Should().Contain("data:image/jpeg;base64,");
        body.Should().Contain("test-model");
    }

    [Fact]
    public void BuildPickPromptShouldListCandidateNames()
    {
        var prompt = VisionModelClientBase.BuildPickPrompt(
            "Super Mario Bros", "Super Mario Bros", CreateImages());

        prompt.Should().Contain("Super Mario Bros");
        prompt.Should().Contain("0: a.png");
        prompt.Should().Contain("1: b.png");
    }

    [Fact]
    public async Task PickBestAsyncShouldPreferCoverOrScreenshotOverCartForApiFallback()
    {
        var body = await CapturePickRequestBodyAsync(AiPickKind.ApiFallback);

        body.Should().Contain("cartridge");
        body.Should().Contain("screenshot");
    }

    [Fact]
    public async Task PickBestAsyncShouldUseCoverOnlyPromptForLocalCandidates()
    {
        var body = await CapturePickRequestBodyAsync(AiPickKind.Local);

        body.Should().Contain("official cover or box art");
        body.Should().NotContain("cartridge");
    }

    private static async Task<string> CapturePickRequestBodyAsync(AiPickKind kind)
    {
        string? body = null;
        using var handler = new StubHttpMessageHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"{\\\"bestIndex\\\":0,\\\"confidence\\\":0.9,\\\"reason\\\":\\\"cover\\\"}\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OpenAiCompatibleVisionClient(httpClient);

        await client.PickBestAsync(
            CreateOptions(), "Super Mario Bros", "Super Mario Bros", CreateImages(), CancellationToken.None, kind);

        body.Should().NotBeNull();
        return body!;
    }

    [Fact]
    public void BuildErrorMessageShouldDescribeCommonStatuses()
    {
        OpenAiCompatibleVisionClient
            .BuildErrorMessage(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"bad key\"}}")
            .Should().Contain("bad key");

        OpenAiCompatibleVisionClient.BuildErrorMessage(HttpStatusCode.TooManyRequests, string.Empty)
            .Should().Contain("429");
    }

    [Fact]
    public async Task PickBestAsyncShouldReturnMappedResultFromProvider()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"{\\\"bestIndex\\\":1,\\\"confidence\\\":0.91,\\\"reason\\\":\\\"box art\\\"}\"}}]}",
                Encoding.UTF8,
                "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OpenAiCompatibleVisionClient(httpClient);

        var result = await client.PickBestAsync(
            CreateOptions(), "Super Mario Bros", "Super Mario Bros", CreateImages(), CancellationToken.None);

        result.BestIndex.Should().Be(1);
        result.Confidence.Should().BeApproximately(0.91, 0.001);
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task PickBestAsyncShouldThrowFriendlyMessageOnUnauthorized()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid key\"}}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OpenAiCompatibleVisionClient(httpClient);

        var act = async () => await client.PickBestAsync(
            CreateOptions(), "game", "game", CreateImages(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task PickBestAsyncShouldRejectEmptyImageList()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        using var client = new OpenAiCompatibleVisionClient(httpClient);

        var act = async () => await client.PickBestAsync(
            CreateOptions(), "game", "game", [], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
