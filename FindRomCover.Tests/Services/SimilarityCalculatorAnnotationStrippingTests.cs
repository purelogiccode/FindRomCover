using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class SimilarityCalculatorAnnotationStrippingTests : IDisposable
{
    private readonly string _testDir;

    public SimilarityCalculatorAnnotationStrippingTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SimilarityAnnotationTests_{Guid.NewGuid():N}");
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

    [Theory]
    [InlineData("007 - Everything or Nothing (Asia) (En)", "007 - Everything or Nothing")]
    [InlineData("Game [v1.0]", "Game")]
    [InlineData("Game {test}", "Game")]
    [InlineData("Game (USA) [v1] {test}", "Game")]
    [InlineData("Game (Japan) (Rev 1)", "Game")]
    [InlineData("Sonic (USA) [En] (Rev A)", "Sonic")]
    [InlineData("Game [Disc 1] [En]", "Game")]
    [InlineData("Game (USA).", "Game")]
    [InlineData("Game", "Game")]
    public void StripAnnotationsShouldRemoveBracketedGroups(string input, string expected)
    {
        var result = SimilarityCalculator.StripAnnotations(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void StripAnnotationsShouldHandleNestedGroups()
    {
        var result = SimilarityCalculator.StripAnnotations("Game (Europe (Rev 1))");

        result.Should().Be("Game");
    }

    [Fact]
    public void StripAnnotationsShouldKeepUnbalancedBrackets()
    {
        var result = SimilarityCalculator.StripAnnotations("Game (USA");

        result.Should().Be("Game (USA");
    }

    [Fact]
    public void StripAnnotationsShouldReturnOriginalWhenEverythingIsBracketed()
    {
        var result = SimilarityCalculator.StripAnnotations("(USA)");

        result.Should().Be("(USA)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StripAnnotationsShouldReturnInputWhenBlank(string input)
    {
        var result = SimilarityCalculator.StripAnnotations(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripAnnotationsShouldReturnNullWhenNull()
    {
        var result = SimilarityCalculator.StripAnnotations(null!);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(AppConstants.Algorithms.JaroWinkler)]
    [InlineData(AppConstants.Algorithms.Levenshtein)]
    [InlineData(AppConstants.Algorithms.Jaccard)]
    public async Task FindTopCandidatesAsyncShouldIgnoreBracketedTextWhenEnabled(string algorithm)
    {
        CreateImage("Game (USA).png");

        var candidates = await SimilarityCalculator.FindTopCandidatesAsync(
            "Game",
            _testDir,
            90,
            algorithm,
            CancellationToken.None,
            5,
            ignoreBracketedText: true);

        candidates.Should().HaveCount(1);
        candidates[0].ImageName.Should().Be("Game (USA)");
    }

    [Fact]
    public async Task FindTopCandidatesAsyncShouldNotIgnoreBracketedTextWhenDisabled()
    {
        CreateImage("Game (USA).png");

        var candidates = await SimilarityCalculator.FindTopCandidatesAsync(
            "Game",
            _testDir,
            90,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            5,
            ignoreBracketedText: false);

        candidates.Should().BeEmpty();
    }

    private void CreateImage(string fileName)
    {
        File.WriteAllBytes(Path.Combine(_testDir, fileName), []);
    }
}
