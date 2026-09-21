using FindRomCover.Services;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services;

public class CoverFileResolverTests : IDisposable
{
    private readonly string _testDir;

    public CoverFileResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"CoverFileResolverTests_{Guid.NewGuid():N}");
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
    public void FindCoverShouldReturnPngPath()
    {
        CreateImage("Super Mario Bros.png");

        var result = CoverFileResolver.FindCover(_testDir, "Super Mario Bros");

        result.Should().Be(Path.Combine(_testDir, "Super Mario Bros.png"));
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    [InlineData(".webp")]
    [InlineData(".avif")]
    public void FindCoverShouldRecognizeSupportedImageFormats(string extension)
    {
        CreateImage("Super Mario Bros" + extension);

        var result = CoverFileResolver.FindCover(_testDir, "Super Mario Bros");

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindCoverShouldRecognizeSanitizedFileName()
    {
        CreateImage("Dragon Quest III - Soshite Densetsu e. (Japan).png");

        var result = CoverFileResolver.FindCover(_testDir, "Dragon Quest III - Soshite Densetsu e... (Japan)");

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindCoverShouldReturnNullWhenNoCoverExists()
    {
        var result = CoverFileResolver.FindCover(_testDir, "Super Mario Bros");

        result.Should().BeNull();
    }

    [Fact]
    public void FindCoverShouldIgnoreUnsupportedExtensions()
    {
        File.WriteAllText(Path.Combine(_testDir, "Super Mario Bros.txt"), "not an image");

        var result = CoverFileResolver.FindCover(_testDir, "Super Mario Bros");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FindCoverShouldReturnNullForEmptyRomName(string romName)
    {
        var result = CoverFileResolver.FindCover(_testDir, romName);

        result.Should().BeNull();
    }

    [Fact]
    public void FindCoverShouldReturnNullForEmptyFolderPath()
    {
        var result = CoverFileResolver.FindCover(string.Empty, "Super Mario Bros");

        result.Should().BeNull();
    }

    private void CreateImage(string fileName)
    {
        using var image = new MagickImage(MagickColors.Red, 16, 16);
        image.Format = MagickFormat.Png;
        image.Write(Path.Combine(_testDir, fileName));
    }
}
