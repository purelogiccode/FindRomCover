using System.Net;
using System.Net.Http;
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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
