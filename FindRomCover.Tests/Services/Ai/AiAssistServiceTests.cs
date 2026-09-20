using System.Net;
using System.Text;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiAssistServiceTests
{
    private static byte[] CreatePngBytes(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.Red, width, height);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    private static string CreateTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai_test_{Guid.NewGuid():N}.png");
        using var image = new MagickImage(MagickColors.Red, 64, 64);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }

    private static AiVerdictCache CreateTempCache()
    {
        return new AiVerdictCache(Path.Combine(Path.GetTempPath(), $"ai_cache_{Guid.NewGuid():N}.json"));
    }

    [Fact]
    public async Task PrepareRemoteCandidatesShouldDownloadThumbnails()
    {
        var png = CreatePngBytes(400, 400);
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(png)
        });
        using var httpClient = new HttpClient(handler);

        var candidates = new List<ImageData>
        {
            new()
            {
                ImagePath = "https://example.com/full1.png",
                ThumbnailUrl = "https://example.com/thumb1.png",
                ImageName = "One"
            },
            new() { ImagePath = "https://example.com/full2.png", ImageName = "Two" }
        };

        var inputs = await AiAssistService.PrepareRemoteCandidatesAsync(
            httpClient, candidates, 6, 128, CancellationToken.None);

        inputs.Should().HaveCount(2);
        inputs[0].Name.Should().Be("One");
        inputs[0].SourceIndex.Should().Be(0);
        inputs[1].SourceIndex.Should().Be(1);

        using var decoded = new MagickImage(inputs[0].Image.JpegBytes);
        decoded.Width.Should().Be(128);
    }

    [Fact]
    public async Task PrepareRemoteCandidatesShouldSkipFailedDownloads()
    {
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("bad", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreatePngBytes(64, 64)) });
        using var httpClient = new HttpClient(handler);

        var candidates = new List<ImageData>
        {
            new() { ImagePath = "https://example.com/bad.png", ImageName = "Bad" },
            new() { ImagePath = "https://example.com/good.png", ImageName = "Good" }
        };

        var inputs = await AiAssistService.PrepareRemoteCandidatesAsync(
            httpClient, candidates, 6, 128, CancellationToken.None);

        inputs.Should().HaveCount(1);
        inputs[0].Name.Should().Be("Good");
        inputs[0].SourceIndex.Should().Be(1);
    }

    [Fact]
    public async Task PrepareRemoteCandidatesShouldRespectMaxCandidates()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(CreatePngBytes(64, 64))
        });
        using var httpClient = new HttpClient(handler);

        var candidates = Enumerable.Range(0, 5)
            .Select(i => new ImageData { ImagePath = $"https://example.com/{i}.png", ImageName = $"Image {i}" })
            .ToList();

        var inputs = await AiAssistService.PrepareRemoteCandidatesAsync(
            httpClient, candidates, 2, 128, CancellationToken.None);

        inputs.Should().HaveCount(2);
        inputs[0].SourceIndex.Should().Be(0);
        inputs[1].SourceIndex.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsyncShouldReturnMatchFromProvider()
    {
        var imagePath = CreateTempImage();
        try
        {
            var settings = new SettingsManager
            {
                AiAssistEnabled = true,
                AiVerifyOnSave = true,
                AiBaseUrl = "https://example.test/v1",
                AiApiKey = "test-key",
                AiModel = "test-model"
            };

            using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"{\\\"match\\\":true,\\\"confidence\\\":0.93,\\\"reason\\\":\\\"cover\\\"}\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            });
            using var httpClient = new HttpClient(handler);
            using var client = new OpenAiCompatibleVisionClient(httpClient);
            using var service = new AiAssistService(settings, client, CreateTempCache(), httpClient);

            var result = await service.VerifyAsync(
                "Super Mario Bros", "Super Mario Bros", imagePath, CancellationToken.None);

            result.Should().NotBeNull();
            result.IsMatch.Should().BeTrue();
            result.Confidence.Should().BeApproximately(0.93, 0.001);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task VerifyAsyncShouldReturnNullWhenAiAssistDisabled()
    {
        var imagePath = CreateTempImage();
        try
        {
            var settings = new SettingsManager { AiAssistEnabled = false, AiVerifyOnSave = true };
            using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler);
            using var client = new OpenAiCompatibleVisionClient(httpClient);
            using var service = new AiAssistService(settings, client, CreateTempCache(), httpClient);

            var result = await service.VerifyAsync("game", "game", imagePath, CancellationToken.None);

            result.Should().BeNull();
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task VerifyAsyncShouldReturnNullWhenVerifyOnSaveDisabled()
    {
        var imagePath = CreateTempImage();
        try
        {
            var settings = new SettingsManager { AiAssistEnabled = true, AiVerifyOnSave = false };
            using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler);
            using var client = new OpenAiCompatibleVisionClient(httpClient);
            using var service = new AiAssistService(settings, client, CreateTempCache(), httpClient);

            var result = await service.VerifyAsync("game", "game", imagePath, CancellationToken.None);

            result.Should().BeNull();
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void PrepareLocalCandidatesShouldSkipMissingFiles()
    {
        var candidates = new List<ImageData>
        {
            new()
            {
                ImagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"),
                ImageName = "Missing"
            }
        };

        var inputs = AiAssistService.PrepareLocalCandidates(candidates, 6, 128);

        inputs.Should().BeEmpty();
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
