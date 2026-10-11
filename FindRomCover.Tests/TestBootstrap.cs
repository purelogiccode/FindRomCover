using System.Runtime.CompilerServices;
using FindRomCover.Managers;
using FindRomCover.Services;

namespace FindRomCover.Tests;

internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Never let tests read, migrate or overwrite the developer's real settings.
        var testSettingsDirectory = Path.Combine(
            Path.GetTempPath(), "FindRomCover_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testSettingsDirectory);
        SettingsManager.DefaultSettingsDirectoryOverride = testSettingsDirectory;

        ErrorLogger.SendToApiOverride = static (_, _) => Task.FromResult(false);
    }
}