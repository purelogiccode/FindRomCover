using System.Runtime.InteropServices;
using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class WebViewExceptionClassifierTests
{
    [Fact]
    public void IsRuntimeUnavailableShouldRecognizeMissingRuntimeMessage()
    {
        var exception = new InvalidOperationException(
            "WebView2 runtime not found or CreateWebViewEnvironmentWithOptionsInternal not exported.");

        WebViewExceptionClassifier.IsRuntimeUnavailable(exception).Should().BeTrue();
    }

    [Fact]
    public void IsRuntimeUnavailableShouldRecognizeMissingLoaderExport()
    {
        var exception = new InvalidOperationException(
            "CreateWebViewEnvironmentWithOptionsInternal is not exported by WebView2Loader.dll.");

        WebViewExceptionClassifier.IsRuntimeUnavailable(exception).Should().BeTrue();
    }

    [Fact]
    public void IsRuntimeUnavailableShouldInspectAggregateExceptions()
    {
        var exception = new AggregateException(
            new InvalidOperationException("Something else."),
            new InvalidOperationException(
                "WebView2 runtime not found or CreateWebViewEnvironmentWithOptionsInternal not exported."));

        WebViewExceptionClassifier.IsRuntimeUnavailable(exception).Should().BeTrue();
    }

    [Fact]
    public void IsRuntimeUnavailableShouldIgnoreUnrelatedFailures()
    {
        var exception = new AggregateException(
            new InvalidOperationException("Something else."),
            new IOException("The file is locked."));

        WebViewExceptionClassifier.IsRuntimeUnavailable(exception).Should().BeFalse();
    }

    [Fact]
    public void IsAdapterLifecycleFailureShouldRecognizeAbortThrownByTheWebViewAdapter()
    {
        var exception = CaptureException(ThrowFromWebViewAdapter);

        WebViewExceptionClassifier.IsAdapterLifecycleFailure(exception).Should().BeTrue();
    }

    [Fact]
    public void IsAdapterLifecycleFailureShouldIgnoreOtherComFailures()
    {
        var exception = CaptureException(static () =>
            throw new COMException("Unexpected failure.", unchecked((int)0x80004005)));

        WebViewExceptionClassifier.IsAdapterLifecycleFailure(exception).Should().BeFalse();
    }

    [Fact]
    public void IsAdapterLifecycleFailureShouldInspectAggregateExceptions()
    {
        var exception = new AggregateException(
            new InvalidOperationException("Something else."),
            CaptureException(ThrowFromWebViewAdapter));

        WebViewExceptionClassifier.IsAdapterLifecycleFailure(exception).Should().BeTrue();
    }

    private static Exception CaptureException(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected the action to throw.");
    }

    private static void ThrowFromWebViewAdapter()
    {
        throw new COMException("Operation aborted.", unchecked((int)0x80004004));
    }
}
