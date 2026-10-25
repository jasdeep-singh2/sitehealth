using System.Net;

namespace SiteHealth.Core.Tests;

// These tests fail until you implement PageFetcher.FetchAsync.
public class PageFetcherTests
{
    private static readonly Uri Url = new("https://example.com/");

    [Fact]
    public async Task Returns_the_page_html()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Hello</body></html>"),
        });
        var fetcher = new PageFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("<html><body>Hello</body></html>", result.Html);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Returns_the_status_and_an_error_when_the_page_is_not_found()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var fetcher = new PageFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Html);
        Assert.Equal(404, result.StatusCode);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Returns_an_error_when_the_site_is_unreachable()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("No such host is known."));
        var fetcher = new PageFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.StatusCode);
        Assert.Equal("No such host is known.", result.Error);
    }

    [Fact]
    public async Task Returns_an_error_when_the_request_times_out()
    {
        // HttpClient signals its own timeout with TaskCanceledException, even though nobody cancelled us.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var fetcher = new PageFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.StatusCode);
        Assert.Equal("Timed out", result.Error);
    }

    [Fact]
    public async Task Lets_cancellation_bubble_up()
    {
        // Ctrl+C is not a fetch failure: the caller asked to stop, so we must not swallow it.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());
        var fetcher = new PageFetcher(new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetcher.FetchAsync(Url, cts.Token));
    }
}
