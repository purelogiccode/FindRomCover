using FindRomCover.Managers;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Managers;

[Collection("SettingsManager")]
public class SettingsManagerTests : IDisposable
{
    private readonly string _settingsDirectory;

    public SettingsManagerTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"SettingsManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_settingsDirectory, true);
        }
        catch
        {
            /* best effort */
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ThumbnailSizeSetWithinRangeShouldUpdateValue()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ThumbnailSize = 200
        };

        settings.ThumbnailSize.Should().Be(200);
    }

    [Theory]
    [InlineData(49, 50)]
    [InlineData(801, 801)]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(10000, 2000)]
    public void ThumbnailSizeSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ThumbnailSize = input
        };

        settings.ThumbnailSize.Should().Be(expected);
    }

    [Fact]
    public void DefaultValuesShouldBeCorrect()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.ThumbnailSize.Should().Be(300);
        settings.SearchEngine.Should().Be("BingWeb");
        settings.BaseTheme.Should().Be("Dark");
        settings.AccentColor.Should().Be("Blue");
        settings.UseMameDescriptions.Should().BeFalse();
        settings.SupportedExtensions.Should().NotBeEmpty();
    }

    [Fact]
    public void SaveAndLoadSettingsShouldPersistValues()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ThumbnailSize = 400,
            SearchEngine = "Google",
            BaseTheme = "Dark",
            AccentColor = "Red",
            UseMameDescriptions = true,
            GoogleKey = "test-key"
        };

        settings.SaveSettings();

        var loadedSettings = new SettingsManager(_settingsDirectory);
        loadedSettings.LoadSettings();

        loadedSettings.ThumbnailSize.Should().Be(400);
        loadedSettings.SearchEngine.Should().Be("Google");
        loadedSettings.BaseTheme.Should().Be("Dark");
        loadedSettings.AccentColor.Should().Be("Red");
        loadedSettings.UseMameDescriptions.Should().BeTrue();
        loadedSettings.GoogleKey.Should().Be("test-key");
    }

    [Fact]
    public void LoadSettingsWhenFileDoesNotExistShouldCreateDefaults()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();

        settings.ThumbnailSize.Should().Be(300);
        settings.SupportedExtensions.Should().NotBeEmpty();
        // Settings are saved to the SQLite settings database
        File.Exists(Path.Combine(_settingsDirectory, "Settings.dat")).Should().BeTrue();
    }
}
