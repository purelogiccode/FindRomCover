using System.Globalization;
using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ScreenshotServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly List<string> _createdPaths = [];

    public ScreenshotServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"FindRomCover_ScreenshotTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        foreach (var path in _createdPaths)
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                /* best effort */
            }

        try
        {
            if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
        }
        catch
        {
            /* best effort */
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetCandidateFoldersShouldPreferTheApplicationFolder()
    {
        var folders = ScreenshotService.GetCandidateFolders();

        folders.Should().HaveCount(2);
        folders[0].Should().Be(Path.Combine(AppContext.BaseDirectory, "Screenshot"));
        folders[1].Should().Be(Path.Combine(AppDataPaths.BaseDirectory, "Screenshot"));
        folders[0].Should().NotBe(folders[1]);
    }

    [Fact]
    public void SaveScreenshotShouldWriteIntoTheFirstFolderAndReturnThePath()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Local);
        var folder = Path.Combine(_testRoot, "primary");

        var path = ScreenshotService.SaveScreenshot(p => File.WriteAllBytes(p, [1, 2, 3]), timestamp, [folder]);

        path.Should().NotBeNull();
        path.Should().StartWith(folder);
        Path.GetFileName(path).Should().Be(
            $"Screenshot_{timestamp.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)}.png");
        File.Exists(path).Should().BeTrue();
        _createdPaths.Add(path);
    }

    [Fact]
    public void SaveScreenshotShouldCreateMissingFolder()
    {
        var folder = Path.Combine(_testRoot, "nested", "folder");

        var path = ScreenshotService.SaveScreenshot(p => File.WriteAllBytes(p, [1]), DateTime.Now, [folder]);

        path.Should().NotBeNull();
        Directory.Exists(folder).Should().BeTrue();
        _createdPaths.Add(path);
    }

    [Fact]
    public void SaveScreenshotShouldFallBackToTheSecondFolderWhenTheFirstFails()
    {
        // Make the first candidate folder impossible to create by placing a file
        // where its parent directory would need to be.
        var blocked = Path.Combine(_testRoot, "blocked-primary");
        File.WriteAllText(blocked, "not a directory");
        var fallback = Path.Combine(_testRoot, "fallback");

        var path = ScreenshotService.SaveScreenshot(
            p => File.WriteAllBytes(p, [1, 2, 3]),
            DateTime.Now,
            [Path.Combine(blocked, "Screenshot"), fallback]);

        path.Should().NotBeNull();
        path.Should().StartWith(fallback);
        File.Exists(path).Should().BeTrue();
        _createdPaths.Add(path);
    }

    [Fact]
    public void SaveScreenshotShouldFallBackWhenTheFirstWriteThrows()
    {
        var primary = Path.Combine(_testRoot, "primary-throws");
        var fallback = Path.Combine(_testRoot, "secondary");
        var attempts = 0;

        var path = ScreenshotService.SaveScreenshot(p =>
            {
                attempts++;
                if (attempts == 1) throw new IOException("simulated failure");
                File.WriteAllBytes(p, [1]);
            },
            DateTime.Now, [primary, fallback]);

        path.Should().NotBeNull();
        path.Should().StartWith(fallback);
        attempts.Should().Be(2);
        _createdPaths.Add(path);
    }

    [Fact]
    public void SaveScreenshotShouldReturnNullWhenEveryFolderFails()
    {
        var folderA = Path.Combine(_testRoot, "fail-a");
        var folderB = Path.Combine(_testRoot, "fail-b");

        var path = ScreenshotService.SaveScreenshot(
            _ => throw new UnauthorizedAccessException("simulated"),
            DateTime.Now,
            [folderA, folderB]);

        path.Should().BeNull();
    }

    [Fact]
    public void SaveScreenshotShouldNotOverwriteAnExistingFile()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Local);
        var folder = Path.Combine(_testRoot, "collisions");
        Directory.CreateDirectory(folder);

        var expectedName = $"Screenshot_{timestamp.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)}.png";
        var existingPath = Path.Combine(folder, expectedName);
        File.WriteAllText(existingPath, "existing");

        var path = ScreenshotService.SaveScreenshot(p => File.WriteAllBytes(p, [9]), timestamp, [folder]);

        path.Should().NotBeNull();
        path.Should().NotBe(existingPath);
        Path.GetFileName(path).Should().Be(
            $"Screenshot_{timestamp.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)}_1.png");
        File.ReadAllText(existingPath).Should().Be("existing");
        _createdPaths.Add(path!);
    }

    [Fact]
    public void SaveScreenshotShouldThrowForNullWriter()
    {
        var act = () => ScreenshotService.SaveScreenshot(null!, DateTime.Now, [_testRoot]);

        act.Should().Throw<ArgumentNullException>();
    }
}
