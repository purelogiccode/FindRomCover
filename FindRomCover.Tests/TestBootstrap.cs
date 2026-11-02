using System.Runtime.CompilerServices;
using FindRomCover.Managers;
using FindRomCover.Services;

namespace FindRomCover.Tests;

internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(), "FindRomCover_Tests", Guid.NewGuid().ToString("N"));

        // Never let tests write into the real %LocalAppData%\FindRomCover folder.
        // ErrorLogger queues reports it cannot send in PendingBugReports.json there, and
        // the next application launch re-sends that queue to the production bug tracker:
        // a test run used to file its fake "test"/marker reports as real bugs.
        var testAppDataDirectory = Path.Combine(testRoot, "AppData");
        Directory.CreateDirectory(testAppDataDirectory);
        AppDataPaths.BaseDirectoryOverride = testAppDataDirectory;

        // Never let tests read, migrate or overwrite the developer's real settings.
        var testSettingsDirectory = Path.Combine(testRoot, "Settings");
        Directory.CreateDirectory(testSettingsDirectory);
        SettingsManager.DefaultSettingsDirectoryOverride = testSettingsDirectory;

        ErrorLogger.SendToApiOverride = static (_, _) => Task.FromResult(false);
    }
}
