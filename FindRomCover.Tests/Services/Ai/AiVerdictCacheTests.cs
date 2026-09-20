using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiVerdictCacheTests : IDisposable
{
    private readonly string _cachePath;

    public AiVerdictCacheTests()
    {
        _cachePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "AiVerdictCacheTests",
            $"cache_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_cachePath)) File.Delete(_cachePath);
        }
        catch
        {
            /* best effort */
        }
    }

    [Fact]
    public void TryGetWithUnknownKeyShouldReturnFalse()
    {
        var cache = new AiVerdictCache(_cachePath);

        cache.TryGet<AiPickResult>("missing", out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void SetThenTryGetShouldReturnValue()
    {
        var cache = new AiVerdictCache(_cachePath);
        cache.Set("key", new AiPickResult { BestIndex = 1, Confidence = 0.8, Reason = "test" });

        cache.TryGet<AiPickResult>("key", out var value).Should().BeTrue();
        value!.BestIndex.Should().Be(1);
        value.Reason.Should().Be("test");
    }

    [Fact]
    public void CacheShouldPersistAcrossInstances()
    {
        var first = new AiVerdictCache(_cachePath);
        first.Set("key", new AiVerificationResult { IsMatch = true, Confidence = 0.7 });

        var second = new AiVerdictCache(_cachePath);
        second.TryGet<AiVerificationResult>("key", out var value).Should().BeTrue();
        value!.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void ExpiredEntriesShouldNotBeReturned()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
        File.WriteAllText(
            _cachePath,
            "{\"old\":{\"CreatedUtc\":\"2000-01-01T00:00:00+00:00\",\"Payload\":\"{\\\"BestIndex\\\":1}\"}}");

        var cache = new AiVerdictCache(_cachePath);

        cache.TryGet<AiPickResult>("old", out _).Should().BeFalse();
    }

    [Fact]
    public void CorruptCacheFileShouldNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
        File.WriteAllText(_cachePath, "not json");

        var cache = new AiVerdictCache(_cachePath);
        cache.TryGet<AiPickResult>("key", out _).Should().BeFalse();

        var act = () => cache.Set("key", new AiPickResult { BestIndex = 0 });
        act.Should().NotThrow();
    }
}
