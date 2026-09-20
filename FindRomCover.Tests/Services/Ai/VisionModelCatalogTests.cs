using System.Net;
using System.Text;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class VisionModelCatalogTests
{
    private static AiVisionOptions CreateOptions(string provider, string baseUrl)
    {
        return new AiVisionOptions(true, provider, baseUrl, "test-key", "test-model", 30, 6, 512, 80, false, false, false);
    }

    [Fact]
    public void ParseModelIdsShouldReadOpenAiAndGeminiShapes()
    {
        var openAi = VisionModelCatalog.ParseModelIds(
            "{\"data\":[{\"id\":\"gpt-4o-mini\"},{\"id\":\"gpt-4o\"}]}");
        var gemini = VisionModelCatalog.ParseModelIds(
            "{\"models\":[{\"name\":\"models/gemini-2.5-flash\"},{\"name\":\"models/gemini-2.5-pro\"}]}");

        openAi.Should().Equal("gpt-4o", "gpt-4o-mini");
        gemini.Should().Equal("gemini-2.5-flash", "gemini-2.5-pro");
    }

    [Fact]
    public void ParseModelIdsShouldDeduplicateAndIgnoreEmptyIds()
    {
        var ids = VisionModelCatalog.ParseModelIds(
            "{\"data\":[{\"id\":\"model-a\"},{\"id\":\"model-a\"},{\"id\":\"\"},{}]}");

        ids.Should().Equal("model-a");
    }

    [Fact]
    public void BuildModelsUrlShouldAppendPath()
    {
        VisionModelCatalog.BuildModelsUrl("https://openrouter.ai/api/v1/")
            .Should().Be("https://openrouter.ai/api/v1/models");
    }

    [Fact]
    public async Task FetchModelIdsAsyncShouldSendBearerTokenForOpenRouter()
    {
        string? authorization = null;
        using var handler = new StubHttpMessageHandler(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return Json("{\"data\":[{\"id\":\"google/gemini-2.5-flash\"}]}");
        });
        using var httpClient = new HttpClient(handler);

        var ids = await VisionModelCatalog.FetchModelIdsAsync(
            CreateOptions(AppConstants.AiProviders.OpenRouter, "https://openrouter.ai/api/v1"),
            httpClient,
            CancellationToken.None);

        ids.Should().Equal("google/gemini-2.5-flash");
        authorization.Should().Be("Bearer test-key");
    }

    [Fact]
    public async Task FetchModelIdsAsyncShouldSendAnthropicKeyHeader()
    {
        string? apiKey = null;
        using var handler = new StubHttpMessageHandler(request =>
        {
            request.Headers.TryGetValues("x-api-key", out var values);
            apiKey = values?.FirstOrDefault();
            return Json("{\"data\":[{\"id\":\"claude-sonnet-4-5\"}]}");
        });
        using var httpClient = new HttpClient(handler);

        var ids = await VisionModelCatalog.FetchModelIdsAsync(
            CreateOptions(AppConstants.AiProviders.Anthropic, "https://api.anthropic.com/v1"),
            httpClient,
            CancellationToken.None);

        ids.Should().Equal("claude-sonnet-4-5");
        apiKey.Should().Be("test-key");
    }

    [Fact]
    public async Task FetchModelIdsAsyncShouldSendGeminiKeyHeader()
    {
        string? apiKey = null;
        using var handler = new StubHttpMessageHandler(request =>
        {
            request.Headers.TryGetValues("x-goog-api-key", out var values);
            apiKey = values?.FirstOrDefault();
            return Json("{\"models\":[{\"name\":\"models/gemini-2.5-flash\"}]}");
        });
        using var httpClient = new HttpClient(handler);

        var ids = await VisionModelCatalog.FetchModelIdsAsync(
            CreateOptions(AppConstants.AiProviders.Gemini, "https://generativelanguage.googleapis.com/v1beta"),
            httpClient,
            CancellationToken.None);

        ids.Should().Equal("gemini-2.5-flash");
        apiKey.Should().Be("test-key");
    }

    [Fact]
    public async Task FetchModelIdsAsyncShouldThrowFriendlyMessageOnUnauthorized()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid key\"}}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);

        var act = async () => await VisionModelCatalog.FetchModelIdsAsync(
            CreateOptions(AppConstants.AiProviders.OpenRouter, "https://openrouter.ai/api/v1"),
            // ReSharper disable once AccessToDisposedClosure
            httpClient,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid key*");
    }

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
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
