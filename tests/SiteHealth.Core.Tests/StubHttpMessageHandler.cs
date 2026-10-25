using System.Collections.Concurrent;

namespace SiteHealth.Core.Tests;

/// <summary>
/// A fake HTTP handler so tests never touch the real network.
/// Pass it to <c>new HttpClient(handler)</c> and decide the response for each request.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    // Thread-safe because LinkChecker sends requests in parallel.
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(respond(request));
    }
}
