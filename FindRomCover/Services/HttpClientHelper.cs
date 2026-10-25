namespace FindRomCover.Services;

/// <summary>
///     Provides the shared <see cref="HttpClient" /> instance used by every network
///     service, with connection pooling configured for long-running desktop use.
/// </summary>
public static class HttpClientHelper
{
    private static readonly Lazy<HttpClient> HttpClient = new(static () =>
    {
        var handler = new SocketsHttpHandler
        {
            // Configure connection pooling
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 10
        };

        var client = new HttpClient(handler)
        {
            // Settings allow API timeouts up to 300 s. A shorter client-wide timeout
            // silently capped every configured value, so use the maximum and let each
            // caller's CancellationTokenSource enforce the real timeout.
            Timeout = TimeSpan.FromSeconds(300)
        };

        // Set default headers if needed
        client.DefaultRequestHeaders.ConnectionClose = false; // Keep connections alive

        return client;
    });

    private static int _disposed;

    /// <summary>
    ///     Gets the lazily created shared HTTP client.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose" /> has been called.</exception>
    public static HttpClient Client
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(HttpClientHelper), "HttpClientHelper has been disposed.");

            return HttpClient.Value;
        }
    }

    /// <summary>
    ///     Disposes the shared client once. Subsequent calls are ignored.
    /// </summary>
    public static void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        if (HttpClient.IsValueCreated)
            try
            {
                HttpClient.Value.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
    }
}