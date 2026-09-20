using FindRomCover.Services.Ai;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class VisionImagePreparerTests : IDisposable
{
    private readonly string _testDir;

    public VisionImagePreparerTests()
    {
        _testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VisionImagePreparerTests");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                /* best effort */
            }

        GC.SuppressFinalize(this);
    }

    private string CreateImage(string fileName, uint width, uint height)
    {
        var path = Path.Combine(_testDir, fileName);
        using var image = new MagickImage(MagickColors.Red, width, height);
        image.Format = MagickFormat.Png;
        image.Write(path);
        return path;
    }

    [Fact]
    public void PrepareFromFileShouldDownscaleLargeImage()
    {
        var path = CreateImage("large.png", 1024, 512);

        var prepared = VisionImagePreparer.Prepare(path, 256);

        using var result = new MagickImage(prepared.JpegBytes);
        result.Format.Should().Be(MagickFormat.Jpeg);
        result.Width.Should().Be(256);
        result.Height.Should().Be(128);
    }

    [Fact]
    public void PrepareFromFileShouldKeepSmallImageSize()
    {
        var path = CreateImage("small.png", 64, 48);

        var prepared = VisionImagePreparer.Prepare(path, 512);

        using var result = new MagickImage(prepared.JpegBytes);
        result.Width.Should().Be(64);
        result.Height.Should().Be(48);
    }

    [Fact]
    public void PrepareShouldReturnStableHashForSameInput()
    {
        var path = CreateImage("stable.png", 100, 100);

        var first = VisionImagePreparer.Prepare(path, 256);
        var second = VisionImagePreparer.Prepare(path, 256);

        first.Hash.Should().Be(second.Hash);
    }

    [Fact]
    public void PrepareFromBytesShouldReturnJpegData()
    {
        byte[] pngBytes;
        using (var image = new MagickImage(MagickColors.Yellow, 800, 600))
        {
            image.Format = MagickFormat.Png;
            pngBytes = image.ToByteArray();
        }

        var prepared = VisionImagePreparer.Prepare(pngBytes, 128);

        using var result = new MagickImage(prepared.JpegBytes);
        result.Width.Should().Be(128);
        result.Height.Should().Be(96);
    }

    [Fact]
    public void PrepareWithMissingFileShouldThrow()
    {
        var act = () => VisionImagePreparer.Prepare(Path.Combine(_testDir, "missing.png"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void PrepareWithEmptyBytesShouldThrow()
    {
        var act = () => VisionImagePreparer.Prepare([], 256);

        act.Should().Throw<ArgumentException>();
    }
}
