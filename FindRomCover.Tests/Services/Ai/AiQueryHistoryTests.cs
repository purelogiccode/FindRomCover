using System.Globalization;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiQueryHistoryTests : IDisposable
{
    private readonly string _historyPath;
    private readonly string _targetPath;

    public AiQueryHistoryTests()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"AiQueryHistoryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _historyPath = Path.Combine(directory, "QueryHistory.dat");
        _targetPath = Path.Combine(directory, "Super Mario Bros.png");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_historyPath)!, true);
        }
        catch
        {
            /* best effort */
        }
    }

    [Fact]
    public void MarkQueriedShouldMakeWasQueriedReturnTrue()
    {
        var history = new AiQueryHistory(_historyPath);

        history.WasQueried(_targetPath).Should().BeFalse();

        history.MarkQueried(_targetPath, "local-no-match");

        history.WasQueried(_targetPath).Should().BeTrue();
        history.Count.Should().Be(1);
    }

    [Fact]
    public void WasQueriedShouldMatchPlatformPathSemantics()
    {
        var history = new AiQueryHistory(_historyPath);
        history.MarkQueried(_targetPath, "manual");

        history.WasQueried(_targetPath.ToUpperInvariant()).Should().Be(OperatingSystem.IsWindows());
    }

    [Fact]
    public void GetQueriedPathSetShouldReturnNormalizedEntries()
    {
        var history = new AiQueryHistory(_historyPath);
        history.MarkQueried(_targetPath, "manual");
        history.MarkQueried(_targetPath + ".second.png", "api-no-match");

        var paths = history.GetQueriedPathSet();

        paths.Should().HaveCount(2);
        paths.Should().Contain(AiQueryHistory.NormalizeKey(_targetPath));
        paths.Should().Contain(AiQueryHistory.NormalizeKey(_targetPath + ".second.png"));
    }

    [Fact]
    public void RemoveShouldForgetEntry()
    {
        var history = new AiQueryHistory(_historyPath);
        history.MarkQueried(_targetPath, "manual");

        history.Remove(_targetPath);

        history.WasQueried(_targetPath).Should().BeFalse();
        history.Count.Should().Be(0);
    }

    [Fact]
    public void ClearShouldRemoveEverything()
    {
        var history = new AiQueryHistory(_historyPath);
        history.MarkQueried(_targetPath, "manual");
        history.MarkQueried(_targetPath + ".second.png", "manual");

        history.Clear();

        history.Count.Should().Be(0);
        history.WasQueried(_targetPath).Should().BeFalse();
    }

    [Fact]
    public void HistoryShouldPersistAcrossInstances()
    {
        var first = new AiQueryHistory(_historyPath);
        first.MarkQueried(_targetPath, "local-no-match");

        var second = new AiQueryHistory(_historyPath);

        second.WasQueried(_targetPath).Should().BeTrue();
        second.Count.Should().Be(1);
    }

    [Fact]
    public void ExistingNoCaseSchemaShouldBeMigratedWithoutLosingRows()
    {
        using (var connection = new SqliteConnection($"Data Source={_historyPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE QueryHistory (TargetPath TEXT PRIMARY KEY COLLATE NOCASE, " +
                "QueriedUtc TEXT NOT NULL, Outcome TEXT NOT NULL);" +
                "INSERT INTO QueryHistory (TargetPath, QueriedUtc, Outcome) " +
                "VALUES ($path, $queried, 'manual');";
            command.Parameters.AddWithValue("$path", AiQueryHistory.NormalizeKey(_targetPath));
            command.Parameters.AddWithValue("$queried",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        var history = new AiQueryHistory(_historyPath);

        history.Count.Should().Be(1);
        history.WasQueried(_targetPath).Should().BeTrue();
    }

    [Fact]
    public void EmptyPathShouldBeIgnored()
    {
        var history = new AiQueryHistory(_historyPath);

        history.MarkQueried(string.Empty, "manual");

        history.WasQueried(string.Empty).Should().BeFalse();
        history.Count.Should().Be(0);
    }
}