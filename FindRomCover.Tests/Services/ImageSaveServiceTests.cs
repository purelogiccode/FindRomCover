using System.Net;
using FindRomCover.Services;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ImageSaveServiceTests : IDisposable
{
    private readonly string _testOutputDir;

    public ImageSaveServiceTests()
    {
        _testOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestOutput");
        if (!Directory.Exists(_testOutputDir)) Directory.CreateDirectory(_testOutputDir);
    }

    public void Dispose()
    {
        // Cleanup test output files
        if (Directory.Exists(_testOutputDir))
            try
            {
                Directory.Delete(_testOutputDir, true);
            }
            catch
            {
                // Best effort cleanup
            }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ConvertStreamToPngAndSaveAsyncWithValidImageStreamShouldSavePng()
    {
        var outputPath = Path.Combine(_testOutputDir, "test_output.png");

        // Create a simple 1x1 red PNG in memory using Magick.NET
        using var image = new MagickImage(MagickColors.Red, 10, 10);
        image.Format = MagickFormat.Png;
        var bytes = image.ToByteArray();
        await using var stream = new MemoryStream(bytes);

        var result = await ImageSaveService.ConvertStreamToPngAndSaveAsync(stream, outputPath);

        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertStreamToPngAndSaveAsyncShouldCreateOutputDirectoryWhenMissing()
    {
        var nestedDir = Path.Combine(_testOutputDir, "nested", "deep");
        var outputPath = Path.Combine(nestedDir, "test.png");

        using var image = new MagickImage(MagickColors.Blue, 5, 5);
        image.Format = MagickFormat.Png;
        var bytes = image.ToByteArray();
        await using var stream = new MemoryStream(bytes);

        var result = await ImageSaveService.ConvertStreamToPngAndSaveAsync(stream, outputPath);

        result.Should().BeTrue();
        Directory.Exists(nestedDir).Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertStreamToPngAndSaveAsyncWithInvalidStreamShouldReturnFalse()
    {
        var outputPath = Path.Combine(_testOutputDir, "invalid.png");

        await using var stream = new MemoryStream([0x00, 0x01, 0x02]);

        var result = await ImageSaveService.ConvertStreamToPngAndSaveAsync(stream, outputPath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndSaveImageAsyncShouldSaveImageAndSendBrowserHeaders()
    {
        var outputPath = Path.Combine(_testOutputDir, "downloaded.png");
        var capturedUserAgent = string.Empty;
        var capturedAccept = string.Empty;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedUserAgent = request.Headers.UserAgent.ToString();
            capturedAccept = string.Join(",", request.Headers.Accept.Select(static a => a.MediaType));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreatePngBytes()) };
        }));

        var result = await ImageSaveService.DownloadAndSaveImageAsync(
            "https://example.test/cover.png", null, outputPath, client, CancellationToken.None);

        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        capturedUserAgent.Should().NotBeNullOrWhiteSpace();
        capturedAccept.Should().Contain("image/");
    }

    [Fact]
    public async Task DownloadAndSaveImageAsyncShouldUseFallbackWhenPrimaryIsForbidden()
    {
        var outputPath = Path.Combine(_testOutputDir, "fallback.png");
        var requestedUris = new List<string>();
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri!.AbsoluteUri.Contains("primary", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreatePngBytes()) };
        }));

        var result = await ImageSaveService.DownloadAndSaveImageAsync(
            "https://example.test/primary.png", "https://example.test/fallback-thumbnail.png", outputPath, client,
            CancellationToken.None);

        result.Should().BeTrue();
        requestedUris.Should().HaveCount(2);
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndSaveImageAsyncShouldReturnFalseWhenBothUrlsFail()
    {
        var outputPath = Path.Combine(_testOutputDir, "failed.png");
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var result = await ImageSaveService.DownloadAndSaveImageAsync(
            "https://example.test/primary.png", "https://example.test/fallback.png", outputPath, client,
            CancellationToken.None);

        result.Should().BeFalse();
        File.Exists(outputPath).Should().BeFalse();
    }

    private static byte[] CreatePngBytes()
    {
        using var image = new MagickImage(MagickColors.Green, 8, 8);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
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