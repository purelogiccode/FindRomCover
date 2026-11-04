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
    public void DefaultBaseDirectoryShouldPointToThePerUserFolder()
    {
        // The default is asserted directly instead of clearing the process-wide
        // override: clearing it made a concurrent logger test queue fake reports in the
        // real %LocalAppData%\FindRomCover folder, which the installed application later
        // uploaded to the production bug tracker.
        AppDataPaths.DefaultBaseDirectory.Should().NotBeNullOrWhiteSpace();
        AppDataPaths.DefaultBaseDirectory.Should().EndWith("FindRomCover");
    }

    [Fact]
    public void ClearingTheOverrideShouldBeIgnored()
    {
        var original = AppDataPaths.BaseDirectoryOverride;
        var overrideDirectory = Path.Combine(
            Path.GetTempPath(), "FindRomCover_Tests", Guid.NewGuid().ToString("N"));

        try
        {
            AppDataPaths.BaseDirectoryOverride = overrideDirectory;
            AppDataPaths.BaseDirectoryOverride = null;

            AppDataPaths.BaseDirectory.Should().Be(overrideDirectory,
                "a test run must never fall back to the real per-user application data folder");
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
