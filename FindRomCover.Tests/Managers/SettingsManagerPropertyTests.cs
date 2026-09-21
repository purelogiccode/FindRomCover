using FindRomCover.Managers;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Managers;

[Collection("SettingsManager")]
public class SettingsManagerPropertyTests : IDisposable
{
    private readonly string _settingsDirectory;

    public SettingsManagerPropertyTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"SettingsManagerPropertyTests_{Guid.NewGuid():N}");
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

    [Theory]
    [InlineData(50)]
    [InlineData(300)]
    [InlineData(2000)]
    public void ImageWidthSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageWidth = value
        };

        settings.ImageWidth.Should().Be(value);
    }

    [Theory]
    [InlineData(49, 50)]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(2001, 2000)]
    [InlineData(5000, 2000)]
    public void ImageWidthSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageWidth = input
        };

        settings.ImageWidth.Should().Be(expected);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(300)]
    [InlineData(2000)]
    public void ImageHeightSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageHeight = value
        };

        settings.ImageHeight.Should().Be(value);
    }

    [Theory]
    [InlineData(49, 50)]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(2001, 2000)]
    [InlineData(5000, 2000)]
    public void ImageHeightSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageHeight = input
        };

        settings.ImageHeight.Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(1000)]
    public void MaxImagesToLoadSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            MaxImagesToLoad = value
        };

        settings.MaxImagesToLoad.Should().Be(value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1001, 1000)]
    public void MaxImagesToLoadSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            MaxImagesToLoad = input
        };

        settings.MaxImagesToLoad.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(20)]
    public void ImageLoaderMaxRetriesSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageLoaderMaxRetries = value
        };

        settings.ImageLoaderMaxRetries.Should().Be(value);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(21, 20)]
    public void ImageLoaderMaxRetriesSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageLoaderMaxRetries = input
        };

        settings.ImageLoaderMaxRetries.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(10000)]
    public void ImageLoaderRetryDelayMillisecondsSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageLoaderRetryDelayMilliseconds = value
        };

        settings.ImageLoaderRetryDelayMilliseconds.Should().Be(value);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10001, 10000)]
    public void ImageLoaderRetryDelayMillisecondsSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageLoaderRetryDelayMilliseconds = input
        };

        settings.ImageLoaderRetryDelayMilliseconds.Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(300)]
    public void ApiTimeoutSecondsSetWithinRangeShouldUpdateValue(int value)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ApiTimeoutSeconds = value
        };

        settings.ApiTimeoutSeconds.Should().Be(value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(301, 300)]
    public void ApiTimeoutSecondsSetOutsideRangeShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ApiTimeoutSeconds = input
        };

        settings.ApiTimeoutSeconds.Should().Be(expected);
    }

    [Fact]
    public void LastImageFolderShouldDefaultToEmpty()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.LastImageFolder.Should().BeEmpty();
    }

    [Fact]
    public void LastImageFolderShouldBeSettable()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            LastImageFolder = @"C:\Images"
        };

        settings.LastImageFolder.Should().Be(@"C:\Images");
    }

    [Fact]
    public void SelectedSimilarityAlgorithmShouldHaveDefaultValue()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.SelectedSimilarityAlgorithm.Should().Be("Jaro-Winkler Distance");
    }

    [Fact]
    public void SelectedSimilarityAlgorithmShouldBeSettable()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            SelectedSimilarityAlgorithm = "Levenshtein Distance"
        };

        settings.SelectedSimilarityAlgorithm.Should().Be("Levenshtein Distance");
    }

    [Fact]
    public void SimilarityThresholdShouldHaveDefaultValue()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.SimilarityThreshold.Should().Be(70);
    }

    [Fact]
    public void SimilarityThresholdShouldBeSettable()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            SimilarityThreshold = 85.5
        };

        settings.SimilarityThreshold.Should().Be(85.5);
    }

    [Fact]
    public void SimilarityThresholdShouldBeClampedTo0()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            SimilarityThreshold = -10
        };

        settings.SimilarityThreshold.Should().Be(0);
    }

    [Fact]
    public void SimilarityThresholdShouldBeClampedTo100()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            SimilarityThreshold = 150
        };

        settings.SimilarityThreshold.Should().Be(100);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void BaseThemeShouldAcceptValidValues(string theme)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            BaseTheme = theme
        };

        settings.BaseTheme.Should().Be(theme);
    }

    [Fact]
    public void BaseThemeWithInvalidValueShouldDefaultToDark()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            BaseTheme = "InvalidTheme"
        };

        settings.BaseTheme.Should().Be("Dark");
    }

    [Fact]
    public void BaseThemeWithEmptyShouldDefaultToDark()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            BaseTheme = ""
        };

        settings.BaseTheme.Should().Be("Dark");
    }

    [Theory]
    [InlineData("Red")]
    [InlineData("Green")]
    [InlineData("Blue")]
    [InlineData("Purple")]
    public void AccentColorShouldAcceptValidValues(string color)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            AccentColor = color
        };

        settings.AccentColor.Should().Be(color);
    }

    [Fact]
    public void AccentColorWithInvalidValueShouldDefaultToBlue()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            AccentColor = "InvalidColor"
        };

        settings.AccentColor.Should().Be("Blue");
    }

    [Fact]
    public void AccentColorWithEmptyShouldDefaultToBlue()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            AccentColor = ""
        };

        settings.AccentColor.Should().Be("Blue");
    }

    [Fact]
    public void UseMameDescriptionsShouldWork()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            UseMameDescriptions = true
        };

        settings.UseMameDescriptions.Should().BeTrue();
    }

    [Fact]
    public void ImageWidthAndHeightShouldBeSetIndependently()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ImageWidth = 500,
            ImageHeight = 400
        };

        settings.ImageWidth.Should().Be(500);
        settings.ImageHeight.Should().Be(400);
    }
}