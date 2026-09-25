using FindRomCover.Services;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services;

public class SimilarityCalculatorCalculateSimilarityTests : IDisposable
{
    private readonly string _testDir;

    public SimilarityCalculatorCalculateSimilarityTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"CalculateSimilarityTests_{Guid.NewGuid():N}");
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
    public async Task CalculateSimilarityAsyncShouldHonorIgnoreBracketedText()
    {
        CreateImage("Super Mario Bros (Europe).png");

        var result = await SimilarityCalculator.CalculateSimilarityAsync(
            "Super Mario Bros (USA)",
            _testDir,
            90,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            5,
            ignoreBracketedText: true);

        result.ProcessingErrors.Should().BeEmpty();
        result.SimilarImages.Should().HaveCount(1);
        result.SimilarImages[0].ImageName.Should().Be("Super Mario Bros (Europe)");
        result.SimilarImages[0].SimilarityScore.Should().Be(100);
    }

    [Fact]
    public async Task CalculateSimilarityAsyncShouldScoreFullNamesWhenIgnoringDisabled()
    {
        CreateImage("Super Mario Bros (Europe).png");

        var result = await SimilarityCalculator.CalculateSimilarityAsync(
            "Super Mario Bros (USA)",
            _testDir,
            99,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            5,
            ignoreBracketedText: false);

        result.ProcessingErrors.Should().BeEmpty();
        result.SimilarImages.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateSimilarityAsyncShouldHonorIgnoreBracketedTextWithTrigramIndex()
    {
        for (var i = 0; i < 60; i++)
            CreateImage($"zzz unrelated {i:00}.png", MagickColors.Blue, 8, 8);

        CreateImage("Super Mario Bros (Europe).png");

        var result = await SimilarityCalculator.CalculateSimilarityAsync(
            "Super Mario Bros (USA)",
            _testDir,
            90,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            5,
            ignoreBracketedText: true);

        result.ProcessingErrors.Should().BeEmpty();
        result.SimilarImages.Should().HaveCount(1);
        result.SimilarImages[0].ImageName.Should().Be("Super Mario Bros (Europe)");
        result.SimilarImages[0].SimilarityScore.Should().Be(100);
    }

    private void CreateImage(string fileName, MagickColor? color = null, uint width = 32, uint height = 32)
    {
        using var image = new MagickImage(color ?? MagickColors.Red, width, height);
        image.Write(Path.Combine(_testDir, fileName));
    }
}
