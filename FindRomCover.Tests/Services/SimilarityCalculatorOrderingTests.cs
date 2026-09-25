using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class SimilarityCalculatorOrderingTests : IDisposable
{
    private readonly string _testDir;

    public SimilarityCalculatorOrderingTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SimilarityOrderingTests_{Guid.NewGuid():N}");
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
    public async Task FindTopCandidatesAsyncShouldReturnSameOrderAcrossRunsWhenScoresTie()
    {
        for (var i = 0; i < 20; i++)
            File.WriteAllBytes(Path.Combine(_testDir, $"game {i:00}.png"), []);

        var runs = new List<List<string>>();
        for (var run = 0; run < 5; run++)
        {
            var candidates = await SimilarityCalculator.FindTopCandidatesAsync(
                "game",
                _testDir,
                50,
                AppConstants.Algorithms.JaroWinkler,
                CancellationToken.None,
                5);

            runs.Add(candidates.Select(static c => c.ImageName).ToList());
        }

        runs.Should().OnlyContain(r => r.SequenceEqual(runs[0]));
        runs[0].Should().Equal("game 00", "game 01", "game 02", "game 03", "game 04");
    }

    [Fact]
    public async Task FindTopCandidatesAsyncShouldReturnAllTiedCandidatesInPathOrder()
    {
        for (var i = 0; i < 10; i++)
            File.WriteAllBytes(Path.Combine(_testDir, $"game {i:00}.png"), []);

        var candidates = await SimilarityCalculator.FindTopCandidatesAsync(
            "game",
            _testDir,
            50,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            20);

        candidates.Select(static c => c.ImageName).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        candidates.Select(static c => c.SimilarityScore).Should().AllBeEquivalentTo(candidates[0].SimilarityScore);
    }
}
