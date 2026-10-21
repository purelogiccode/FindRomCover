using System.Runtime.InteropServices;

namespace FindRomCover.Services;

/// <summary>
///     Classifies exceptions thrown by Avalonia's native WebView adapters so the global
///     exception handlers can distinguish framework/environment failures from application
///     bugs.
/// </summary>
internal static class WebViewExceptionClassifier
{
    private const string RuntimeMissingMarker = "WebView2 runtime not found";
    private const string LoaderExportMarker = "CreateWebViewEnvironmentWithOptionsInternal";
    private const string WebViewStackMarker = "WebView";

    /// <summary>
    ///     Detects the WebView2 adapter lifecycle failures reported by issues #68251, #68252,
    ///     #68285 and #68286: Avalonia's WebView2 adapter throws a COM exception (E_ABORT or
    ///     ERROR_INVALID_STATE) from its asynchronous initialization or from a pending
    ///     size-change notification after its controller was already closed.
    /// </summary>
    /// <param name="exception">The exception (possibly an AggregateException) to inspect.</param>
    /// <returns><see langword="true" /> when the failure is a known WebView adapter lifecycle race.</returns>
    internal static bool IsAdapterLifecycleFailure(Exception? exception)
    {
        foreach (var current in Enumerate(exception))
            if (current is COMException comException &&
                (uint)comException.HResult is 0x80004004 or 0x8007139F &&
                comException.StackTrace?.Contains(WebViewStackMarker, StringComparison.OrdinalIgnoreCase) == true)
                return true;

        return false;
    }

    /// <summary>
    ///     Detects a missing WebView2 runtime or a WebView2 loader that does not export the
    ///     environment factory, reported by issues #68439 and #68440. This is an environment
    ///     problem on the user's machine, not an application bug.
    /// </summary>
    /// <param name="exception">The exception (possibly an AggregateException) to inspect.</param>
    /// <returns><see langword="true" /> when the WebView2 runtime or its loader is unavailable.</returns>
    internal static bool IsRuntimeUnavailable(Exception? exception)
    {
        foreach (var current in Enumerate(exception))
        {
            if (current is not InvalidOperationException) continue;

            if (current.Message.Contains(RuntimeMissingMarker, StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(LoaderExportMarker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<Exception> Enumerate(Exception? exception)
    {
        if (exception is null) yield break;

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                foreach (var nested in Enumerate(innerException))
                    yield return nested;
            }

            yield break;
        }

        yield return exception;

        foreach (var inner in Enumerate(exception.InnerException))
            yield return inner;
    }
}
