using FindRomCover.Models;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Models;

public class ExceptionDetailsTests
{
    [Fact]
    public void FromExceptionShouldPopulateType()
    {
        var ex = new InvalidOperationException("test");

        var details = ExceptionDetails.FromException(ex);

        details.Type.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void FromExceptionShouldPopulateMessage()
    {
        var ex = new InvalidOperationException("custom message");

        var details = ExceptionDetails.FromException(ex);

        details.Message.Should().Be("custom message");
    }

    [Fact]
    public void FromExceptionShouldPopulateSource()
    {
        var ex = new InvalidOperationException("test") { Source = "TestModule" };

        var details = ExceptionDetails.FromException(ex);

        details.Source.Should().Be("TestModule");
    }

    [Fact]
    public void FromExceptionWithNullSourceShouldReturnUnknown()
    {
        var ex = new InvalidOperationException("test")
        {
            Source = null
        };

        var details = ExceptionDetails.FromException(ex);

        details.Source.Should().Be("Unknown");
    }

    [Fact]
    public void FromExceptionWithInnerExceptionShouldPopulateInnerExceptionDetails()
    {
        // ReSharper disable once NotResolvedInText
        var inner = new ArgumentException("inner error", "testParam");
        var outer = new InvalidOperationException("outer error", inner);

        var details = ExceptionDetails.FromException(outer);

        details.InnerException.Should().NotBeNull();
        details.InnerException!.Type.Should().Contain("ArgumentException");
        // Since .NET Core 3.0, ArgumentException.Message includes the parameter name,
        // e.g. "inner error (Parameter 'testParam')". Assert the core message only so
        // the test does not depend on the runtime's exact message formatting.
        details.InnerException.Message.Should().StartWith("inner error");
    }

    [Fact]
    public void FromExceptionWithoutInnerExceptionShouldHaveNullInnerException()
    {
        var ex = new InvalidOperationException("test");

        var details = ExceptionDetails.FromException(ex);

        details.InnerException.Should().BeNull();
    }

    [Fact]
    public void FromExceptionWithDeeplyNestedInnerExceptionShouldRecursivelyPopulateAllLevels()
    {
        // ReSharper disable once NotResolvedInText
        var level2 = new ArgumentNullException("parameterName", "null param");
        var level1 = new InvalidOperationException("level1", level2);
        var outer = new InvalidOperationException("outer", level1);

        var details = ExceptionDetails.FromException(outer);

        details.InnerException.Should().NotBeNull();
        details.InnerException!.InnerException.Should().NotBeNull();
        details.InnerException.InnerException!.Type.Should().Contain("ArgumentNullException");
    }

    [Fact]
    public void ToStringShouldContainTypeAndMessageAndSource()
    {
        var ex = new InvalidOperationException("test error") { Source = "TestSource" };
        var details = ExceptionDetails.FromException(ex);

        var result = details.ToString();

        result.Should().Contain("Type:");
        result.Should().Contain("InvalidOperationException");
        result.Should().Contain("Message: test error");
        result.Should().Contain("Source: TestSource");
        result.Should().Contain("StackTrace:");
    }

    [Fact]
    public void ToStringWithInnerExceptionShouldContainInnerExceptionSection()
    {
        var inner = new ArgumentException("inner");
        var outer = new InvalidOperationException("outer", inner);
        var details = ExceptionDetails.FromException(outer);

        var result = details.ToString();

        result.Should().Contain("--- Inner Exception ---");
        result.Should().Contain("ArgumentException");
        result.Should().Contain("inner");
    }

    [Fact]
    public void DefaultValuesShouldBeEmptyStrings()
    {
        var details = new ExceptionDetails();

        details.Type.Should().BeEmpty();
        details.Message.Should().BeEmpty();
        details.Source.Should().BeEmpty();
        details.StackTrace.Should().BeEmpty();
        details.InnerException.Should().BeNull();
    }
}