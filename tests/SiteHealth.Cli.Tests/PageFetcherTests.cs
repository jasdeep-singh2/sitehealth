using System.Net;

namespace SiteHealth.Cli.Tests;

// Step 1: these tests fail until you implement PageFetcher.FetchHtmlAsync.
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

        var html = await fetcher.FetchHtmlAsync(Url, CancellationToken.None);

        Assert.Equal("<html><body>Hello</body></html>", html);
    }

    [Fact]
    public async Task Throws_when_the_page_is_not_found()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var fetcher = new PageFetcher(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchHtmlAsync(Url, CancellationToken.None));
    }
}
