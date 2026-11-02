using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class AppDataPathsTests
{
    [Fact]
    public void BaseDirectoryShouldUseTheOverrideWhenSet()
    {
        var original = AppDataPaths.BaseDirectoryOverride;
        var overrideDirectory = Path.Combine(
            Path.GetTempPath(), "FindRomCover_Tests", Guid.NewGuid().ToString("N"));

        try
        {
            AppDataPaths.BaseDirectoryOverride = overrideDirectory;

            AppDataPaths.BaseDirectory.Should().Be(overrideDirectory);
            AppDataPaths.LogFilePath("app.log").Should().Be(Path.Combine(overrideDirectory, "app.log"));
        }
        finally
        {
            AppDataPaths.BaseDirectoryOverride = original;
        }
    }

    [Fact]
    public void BaseDirectoryShouldFallBackToThePerUserFolderWhenTheOverrideIsCleared()
    {
        var original = AppDataPaths.BaseDirectoryOverride;

        try
        {
            AppDataPaths.BaseDirectoryOverride = null;

            AppDataPaths.BaseDirectory.Should().NotBeNullOrWhiteSpace();
            AppDataPaths.BaseDirectory.Should().EndWith("FindRomCover");
        }
        finally
        {
            AppDataPaths.BaseDirectoryOverride = original;
        }
    }

    [Fact]
    public void EnsureBaseDirectoryShouldCreateTheOverriddenFolder()
    {
        var original = AppDataPaths.BaseDirectoryOverride;
        var overrideDirectory = Path.Combine(
            Path.GetTempPath(), "FindRomCover_Tests", Guid.NewGuid().ToString("N"));

        try
        {
            AppDataPaths.BaseDirectoryOverride = overrideDirectory;

            AppDataPaths.EnsureBaseDirectory();

            Directory.Exists(overrideDirectory).Should().BeTrue();
        }
        finally
        {
            AppDataPaths.BaseDirectoryOverride = original;
            if (Directory.Exists(overrideDirectory)) Directory.Delete(overrideDirectory, true);
        }
    }

    [Fact]
    public void TestsShouldNeverUseTheRealApplicationDataFolder()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData)) return;

        // Regression test: writing to the real folder queues fake bug reports in
        // PendingBugReports.json, which the next application launch uploads to the
        // production bug tracker.
        AppDataPaths.BaseDirectory.Should().NotBe(
            Path.Combine(localApplicationData, "FindRomCover"),
            "test runs must never touch the real per-user application data folder");
    }
}
