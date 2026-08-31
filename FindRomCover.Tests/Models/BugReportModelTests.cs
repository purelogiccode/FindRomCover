using FindRomCover.Models;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Models;

public class BugReportModelTests
{
    [Fact]
    public void DefaultApplicationNameShouldBeFindRomCover()
    {
        var model = new BugReportModel();

        model.ApplicationName.Should().Be("FindRomCover");
    }

    [Fact]
    public void DefaultApplicationVersionShouldBeUnknown()
    {
        var model = new BugReportModel();

        model.ApplicationVersion.Should().Be("Unknown");
    }

    [Fact]
    public void DefaultOsVersionShouldBeUnknown()
    {
        var model = new BugReportModel();

        model.OsVersion.Should().Be("Unknown");
    }

    [Fact]
    public void DefaultArchitectureShouldBeUnknown()
    {
        var model = new BugReportModel();

        model.Architecture.Should().Be("Unknown");
    }

    [Fact]
    public void DefaultBitnessShouldBeUnknown()
    {
        var model = new BugReportModel();

        model.Bitness.Should().Be("Unknown");
    }

    [Fact]
    public void DefaultErrorMessageShouldBeEmpty()
    {
        var model = new BugReportModel();

        model.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void FromExceptionWithNullExceptionShouldReturnValidModel()
    {
        var model = BugReportModel.FromException(null);

        model.Should().NotBeNull();
        model.ApplicationName.Should().Be("FindRomCover");
        model.Exception.Should().NotBeNull();
    }

    [Fact]
    public void FromExceptionWithValidExceptionShouldPopulateExceptionDetails()
    {
        var ex = new InvalidOperationException("Test error");

        var model = BugReportModel.FromException(ex);

        model.Exception.Type.Should().Contain("InvalidOperationException");
        model.Exception.Message.Should().Be("Test error");
    }

    [Fact]
    public void FromExceptionWithContextMessageShouldSetErrorMessage()
    {
        var ex = new InvalidOperationException("Test error");

        var model = BugReportModel.FromException(ex, "User clicked button");

        model.ErrorMessage.Should().Be("User clicked button");
    }

    [Fact]
    public void FromExceptionShouldPopulateEnvironmentDetails()
    {
        var model = BugReportModel.FromException(new InvalidOperationException("test"));

        model.OsVersion.Should().NotBeNullOrEmpty();
        model.Architecture.Should().NotBeNullOrEmpty();
        model.Bitness.Should().NotBeNullOrEmpty();
        model.ProcessorCount.Should().BeGreaterThan(0);
        model.BaseDirectory.Should().NotBeNullOrEmpty();
        model.TempPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToStringShouldContainEnvironmentDetailsSection()
    {
        var model = BugReportModel.FromException(new InvalidOperationException("test error"));

        var result = model.ToString();

        result.Should().Contain("=== Environment Details ===");
        result.Should().Contain("Application Name: FindRomCover");
    }

    [Fact]
    public void ToStringShouldContainErrorDetailsSection()
    {
        var model = BugReportModel.FromException(new InvalidOperationException("test error"), "context message");

        var result = model.ToString();

        result.Should().Contain("=== Error Details ===");
        result.Should().Contain("Error Message: context message");
    }

    [Fact]
    public void ToStringShouldContainExceptionDetailsSection()
    {
        var model = BugReportModel.FromException(new InvalidOperationException("test error"));

        var result = model.ToString();

        result.Should().Contain("=== Exception Details ===");
        result.Should().Contain("Type:");
        result.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void ToStringWithoutContextMessageShouldNotContainErrorMessage()
    {
        var model = BugReportModel.FromException(new InvalidOperationException("test"));

        var result = model.ToString();

        result.Should().Contain("=== Error Details ===");
    }
}