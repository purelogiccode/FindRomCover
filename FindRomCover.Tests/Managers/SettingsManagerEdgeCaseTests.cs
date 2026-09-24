using System.Text;
using System.Text.Json;
using FindRomCover.Managers;
using FindRomCover.Models;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Managers;

[Collection("SettingsManager")]
public class SettingsManagerEdgeCaseTests : IDisposable
{
    private readonly string _settingsDatabasePath;
    private readonly string _settingsDirectory;

    public SettingsManagerEdgeCaseTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"SettingsManagerEdgeTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);
        _settingsDatabasePath = Path.Combine(_settingsDirectory, "Settings.dat");
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
    public void ThumbnailSizeSetToMinBoundaryShouldSucceed()
    {
        var settings = new SettingsManager(_settingsDirectory) { ThumbnailSize = 50 };

        settings.ThumbnailSize.Should().Be(50);
    }

    [Fact]
    public void ThumbnailSizeSetToMaxBoundaryShouldSucceed()
    {
        var settings = new SettingsManager(_settingsDirectory) { ThumbnailSize = 800 };

        settings.ThumbnailSize.Should().Be(800);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(49, 50)]
    [InlineData(801, 801)]
    [InlineData(10000, 2000)]
    public void ThumbnailSizeSetToInvalidValueShouldBeClamped(int input, int expected)
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ThumbnailSize = input
        };

        settings.ThumbnailSize.Should().Be(expected);
    }

    [Fact]
    public void SupportedExtensionsShouldBePersistedAfterSaveAndLoad()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();
        settings.SupportedExtensions = ["zip", "nes", "gba"];
        settings.SaveSettings();

        var loaded = new SettingsManager(_settingsDirectory);
        loaded.LoadSettings();

        loaded.SupportedExtensions.Should().Contain("zip");
        loaded.SupportedExtensions.Should().Contain("nes");
        loaded.SupportedExtensions.Should().Contain("gba");
    }

    [Fact]
    public void LoadSettingsWithCorruptedDataShouldRevertToDefaults()
    {
        // Write corrupted data to the settings database
        File.WriteAllText(_settingsDatabasePath, "this is not valid encrypted data at all !!!");

        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();

        // Should revert to defaults
        settings.ThumbnailSize.Should().Be(300);
        settings.SearchEngine.Should().Be("BingWeb");
        settings.BaseTheme.Should().Be("Dark");
        settings.AccentColor.Should().Be("Blue");
        settings.UseMameDescriptions.Should().BeFalse();
        settings.SupportedExtensions.Should().NotBeEmpty();
    }

    [Fact]
    public void LoadSettingsWithEmptyFileShouldRevertToDefaults()
    {
        File.WriteAllText(_settingsDatabasePath, "");

        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();

        settings.ThumbnailSize.Should().Be(300);
        settings.SearchEngine.Should().Be("BingWeb");
    }

    [Fact]
    public void LegacyEncryptedSettingsShouldBeMigratedToSqliteDatabase()
    {
        var legacyData = new SettingsData
        {
            SimilarityThreshold = 85,
            SearchEngine = "Google",
            AiProvider = AppConstants.AiProviders.OpenAi,
            AiModel = "gpt-4o-mini",
            AiApiKey = "legacy-secret",
            GoogleKey = "legacy-google-key",
            SupportedExtensions = ["zip", "nes"]
        };

        var json = JsonSerializer.Serialize(legacyData);
        File.WriteAllBytes(_settingsDatabasePath, SettingsManager.Encrypt(Encoding.UTF8.GetBytes(json)));

        var settings = new SettingsManager(_settingsDirectory);

        settings.SimilarityThreshold.Should().Be(85);
        settings.SearchEngine.Should().Be("Google");
        settings.AiProvider.Should().Be(AppConstants.AiProviders.OpenAi);
        settings.AiModel.Should().Be("gpt-4o-mini");
        settings.AiApiKey.Should().Be("legacy-secret");
        settings.GoogleKey.Should().Be("legacy-google-key");
        settings.SupportedExtensions.Should().Contain("nes");

        // The legacy file is backed up and the database is now a real SQLite file
        File.Exists(_settingsDatabasePath + ".legacy").Should().BeTrue();
        var header = File.ReadAllBytes(_settingsDatabasePath).Take(16).ToArray();
        Encoding.ASCII.GetString(header).Should().StartWith("SQLite format 3");
    }

    [Fact]
    public void MultipleSaveAndLoadShouldPreserveSettings()
    {
        var settings = new SettingsManager(_settingsDirectory)
        {
            ThumbnailSize = 400,
            SearchEngine = "Google",
            BaseTheme = "Dark",
            AccentColor = "Red"
        };
        settings.SaveSettings();

        var loaded1 = new SettingsManager(_settingsDirectory);
        loaded1.LoadSettings();
        loaded1.ThumbnailSize = 600;
        loaded1.SaveSettings();

        var loaded2 = new SettingsManager(_settingsDirectory);
        loaded2.LoadSettings();

        loaded2.ThumbnailSize.Should().Be(600);
        loaded2.SearchEngine.Should().Be("Google");
        loaded2.BaseTheme.Should().Be("Dark");
    }

    [Fact]
    public void GoogleKeyShouldBePersisted()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();
        settings.GoogleKey = "my-secret-api-key-12345";
        settings.SaveSettings();

        var loaded = new SettingsManager(_settingsDirectory);
        loaded.LoadSettings();

        loaded.GoogleKey.Should().Be("my-secret-api-key-12345");
    }

    [Fact]
    public void UseMameDescriptionsShouldBePersisted()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();
        settings.UseMameDescriptions = true;
        settings.SaveSettings();

        var loaded = new SettingsManager(_settingsDirectory);
        loaded.LoadSettings();

        loaded.UseMameDescriptions.Should().BeTrue();
    }

    [Fact]
    public void IgnoreBracketedTextShouldBePersisted()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();
        settings.IgnoreBracketedText = false;
        settings.SaveSettings();

        var loaded = new SettingsManager(_settingsDirectory);
        loaded.LoadSettings();

        loaded.IgnoreBracketedText.Should().BeFalse();
    }

    [Fact]
    public void TryLoadAllShouldReportFailureWhenDatabaseCannotBeOpened()
    {
        var directoryAsDatabase = Path.Combine(_settingsDirectory, "not-a-database");
        Directory.CreateDirectory(directoryAsDatabase);

        var database = new SettingsDatabase(directoryAsDatabase);

        database.TryLoadAll(out var values).Should().BeFalse();
        values.Should().BeEmpty();
    }

    [Fact]
    public void BugReportApiKeyShouldHaveDefaultValue()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.BugReportApiKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BugReportApiUrlShouldHaveDefaultValue()
    {
        var settings = new SettingsManager(_settingsDirectory);

        settings.BugReportApiUrl.Should().Contain("purelogiccode.com");
    }

    [Fact]
    public void BugReportApiKeyShouldBePersisted()
    {
        var settings = new SettingsManager(_settingsDirectory);
        settings.LoadSettings();
        settings.BugReportApiKey = "custom-key";
        settings.SaveSettings();

        var loaded = new SettingsManager(_settingsDirectory);
        loaded.LoadSettings();

        loaded.BugReportApiKey.Should().Be("custom-key");
    }
}
