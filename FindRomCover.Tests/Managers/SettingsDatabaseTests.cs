using FindRomCover.Managers;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Managers;

public sealed class SettingsDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;

    public SettingsDatabaseTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"SettingsDatabaseTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "Settings.dat");
    }

    public void Dispose()
    {
        try
        {
            MakeWritable(_databasePath);
            Directory.Delete(_directory, true);
        }
        catch
        {
            // Best effort.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TryLoadAllShouldReadAReadOnlyDatabase()
    {
        var database = new SettingsDatabase(_databasePath);
        database.SaveAll(new Dictionary<string, string>(StringComparer.Ordinal) { ["Key"] = "Value" })
            .Should().BeTrue();

        MakeReadOnly(_databasePath);

        var readOnlyDatabase = new SettingsDatabase(_databasePath);
        var loaded = readOnlyDatabase.TryLoadAll(out var values, out var isCorrupt);

        loaded.Should().BeTrue();
        isCorrupt.Should().BeFalse();
        values["Key"].Should().Be("Value");
    }

    private static void MakeReadOnly(string path)
    {
        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.ReadOnly);
        else
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static void MakeWritable(string path)
    {
        if (!File.Exists(path)) return;

        if (OperatingSystem.IsWindows())
            File.SetAttributes(path, FileAttributes.Normal);
        else
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }
}
