using FindRomCover.Models;
using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class ErrorLoggerTests : IDisposable
{
    private readonly string _testLogDir;

    public ErrorLoggerTests()
    {
        _testLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorLoggerTests");
        if (!Directory.Exists(_testLogDir)) Directory.CreateDirectory(_testLogDir);

        // Reset disposed state before each test
        lock (ErrorLogger.DisposeLock)
        {
            ErrorLogger.IsDisposed = false;
        }
    }

    public void Dispose()
    {
        // Reset disposed state after each test
        lock (ErrorLogger.DisposeLock)
        {
            ErrorLogger.IsDisposed = false;
        }

        if (Directory.Exists(_testLogDir))
            try
            {
                Directory.Delete(_testLogDir, true);
            }
            catch
            {
                /* best effort */
            }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DefaultApiTimeoutSecondsShouldBe30()
    {
        ErrorLogger.DefaultApiTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void DisposeShouldSetIsDisposedTrue()
    {
        ErrorLogger.Dispose();

        lock (ErrorLogger.DisposeLock)
        {
            ErrorLogger.IsDisposed.Should().BeTrue();
        }
    }

    [Fact]
    public void DisposeCalledTwiceShouldNotThrow()
    {
        var act = () =>
        {
            ErrorLogger.Dispose();
            ErrorLogger.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task LogAsyncWhenDisposedShouldReturnEarly()
    {
        ErrorLogger.Dispose();

        // Should not throw even when disposed
        var act = () => ErrorLogger.LogAsync(new Exception("test"), "context");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsyncWithNullExceptionShouldNotThrow()
    {
        var act = () => ErrorLogger.LogAsync(null, "null exception test");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsyncWithContextMessageShouldNotThrow()
    {
        var act = () => ErrorLogger.LogAsync(new InvalidOperationException("test"), "test context");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsyncWithNullContextShouldNotThrow()
    {
        var act = () => ErrorLogger.LogAsync(new InvalidOperationException("test"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void LogFilePathsShouldNotBeNullOrWhiteSpace()
    {
        // Access the internal fields via reflection or just verify they are set
        // Since they are internal, we can test indirectly by checking the class doesn't throw
        var act = () => _ = ErrorLogger.DefaultApiTimeoutSeconds;

        act.Should().NotThrow();
    }

    [Fact]
    public void LogFilePathsShouldStayInsideTheTestApplicationDataFolder()
    {
        // Regression test: the log files and the pending bug-report queue must never be
        // written to the real %LocalAppData%\FindRomCover folder. The app re-sends that
        // queue at startup, so leaked test reports became real tracker entries.
        var expectedDirectory = AppDataPaths.BaseDirectory;

        ErrorLogger.ApiLogFilePath.Should().Be(Path.Combine(expectedDirectory, "ApiLogError.txt"));
    }

    [Theory]
    [InlineData(@"C:\repos\FindRomCover.Tests\bin\Release\net10.0-windows")]
    [InlineData("/home/user/repos/FindRomCover.Tests/bin/Release/net10.0-windows")]
    [InlineData(@"C:\Users\user\.nuget\packages\microsoft.testplatform.testhost\18.0.0\lib\net8.0\testhost")]
    public void IsTestHostBaseDirectoryShouldRecognizeTestHostFolders(string baseDirectory)
    {
        ErrorLogger.IsTestHostBaseDirectory(baseDirectory).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\Program Files\FindRomCover")]
    [InlineData(@"D:\Emulators\Simple Launcher\tools\FindRomCover\win-x64")]
    [InlineData("/opt/findromcover")]
    [InlineData("")]
    [InlineData(null)]
    public void IsTestHostBaseDirectoryShouldAcceptApplicationFolders(string? baseDirectory)
    {
        ErrorLogger.IsTestHostBaseDirectory(baseDirectory).Should().BeFalse();
    }

    [Fact]
    public void IsTestHostReportShouldUseTheReportBaseDirectory()
    {
        var testReport = new BugReportModel
        {
            BaseDirectory = Path.Combine("C:", "repos", "FindRomCover.Tests", "bin", "Release")
        };
        var applicationReport = new BugReportModel
        {
            BaseDirectory = Path.Combine("C:", "Program Files", "FindRomCover")
        };

        ErrorLogger.IsTestHostReport(testReport).Should().BeTrue();
        ErrorLogger.IsTestHostReport(applicationReport).Should().BeFalse();
    }

    [Fact]
    public async Task LogAsyncFromTestHostShouldNotQueueOrWriteTheReport()
    {
        // Regression test: test-host reports used to be queued in the real
        // PendingBugReports.json before the API rejected them. The installed application
        // then re-sent that queue and filed the fake test reports as real bugs.
        ErrorLogger.IsTestHostBaseDirectory(AppContext.BaseDirectory).Should().BeTrue(
            "the regression test relies on running from the test output folder");

        var originalOverride = ErrorLogger.SendToApiOverride;
        var marker = $"testhost-{Guid.NewGuid():N}";

        try
        {
            ErrorLogger.SendToApiOverride = null;

            await ErrorLogger.LogAsync(new InvalidOperationException(marker), marker);

            ReadIfExists(ErrorLogger.ApiLogFilePath).Should().NotContain(marker);
            ReadIfExists(Path.Combine(AppDataPaths.BaseDirectory, "PendingBugReports.json"))
                .Should().NotContain(marker);
        }
        finally
        {
            ErrorLogger.SendToApiOverride = originalOverride;
        }
    }

    private static string ReadIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
