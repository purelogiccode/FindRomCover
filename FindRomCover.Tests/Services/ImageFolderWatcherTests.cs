using FindRomCover.Services;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ImageFolderWatcherTests : IDisposable
{
    private readonly string _testDir;

    public ImageFolderWatcherTests()
    {
        _testDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ImageFolderWatcherTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
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

    [Fact]
    public async Task ConvertToPngAsyncShouldConvertJpegAndDeleteSource()
    {
        var sourcePath = Path.Combine(_testDir, "cover.jpg");
        WriteImage(sourcePath, MagickFormat.Jpeg);

        var (convertedPath, error) = await ImageFolderWatcher.ConvertToPngAsync(sourcePath);

        error.Should().BeNull();
        var expectedPath = Path.Combine(_testDir, "cover.png");
        convertedPath.Should().Be(expectedPath);
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(expectedPath).Should().BeTrue();
        ImageProcessor.IsValidPng(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertToPngAsyncShouldNormalizeJpegContentSavedWithPngExtension()
    {
        var sourcePath = Path.Combine(_testDir, "cover.png");
        WriteImage(sourcePath, MagickFormat.Jpeg);

        var (convertedPath, error) = await ImageFolderWatcher.ConvertToPngAsync(sourcePath);

        error.Should().BeNull();
        convertedPath.Should().Be(sourcePath);
        ImageProcessor.IsValidPng(sourcePath).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertToPngAsyncShouldConvertWebpContentSavedWithJpgExtension()
    {
        var sourcePath = Path.Combine(_testDir, "cover.jpg");
        WriteImage(sourcePath, MagickFormat.WebP);

        var (convertedPath, error) = await ImageFolderWatcher.ConvertToPngAsync(sourcePath);

        error.Should().BeNull();
        var expectedPath = Path.Combine(_testDir, "cover.png");
        convertedPath.Should().Be(expectedPath);
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(expectedPath).Should().BeTrue();
        ImageProcessor.IsValidPng(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConvertToPngAsyncShouldLeaveValidPngUntouched()
    {
        var sourcePath = Path.Combine(_testDir, "valid.png");
        WriteImage(sourcePath, MagickFormat.Png);
        var before = await File.ReadAllBytesAsync(sourcePath);

        var (convertedPath, error) = await ImageFolderWatcher.ConvertToPngAsync(sourcePath);

        error.Should().BeNull();
        convertedPath.Should().Be(sourcePath);
        (await File.ReadAllBytesAsync(sourcePath)).Should().Equal(before);
    }

    [Fact]
    public async Task ConvertToPngAsyncShouldReturnErrorForCorruptFile()
    {
        var sourcePath = Path.Combine(_testDir, "cover.jpg");
        await File.WriteAllTextAsync(sourcePath, "this is not an image");

        var (convertedPath, error) = await ImageFolderWatcher.ConvertToPngAsync(sourcePath);

        convertedPath.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
        File.Exists(sourcePath).Should().BeTrue();
    }

    [Fact]
    public void IsValidPngShouldRejectMislabeledFile()
    {
        var sourcePath = Path.Combine(_testDir, "mislabeled.png");
        WriteImage(sourcePath, MagickFormat.Jpeg);

        ImageProcessor.IsValidPng(sourcePath).Should().BeFalse();
    }

    [Fact]
    public void IsValidPngShouldAcceptPngFile()
    {
        var sourcePath = Path.Combine(_testDir, "valid.png");
        WriteImage(sourcePath, MagickFormat.Png);

        ImageProcessor.IsValidPng(sourcePath).Should().BeTrue();
    }

    private static void WriteImage(string path, MagickFormat format)
    {
        using var image = new MagickImage(MagickColors.Red, 16, 16);
        using var stream = File.Create(path);
        image.Write(stream, format);
    }
}
