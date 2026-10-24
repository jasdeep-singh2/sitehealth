using System.Net;
using Microsoft.Extensions.Options;

namespace SiteHealth.Cli.Tests;

// Step 3: these tests fail until you implement LinkChecker.CheckAsync.
public class LinkCheckerTests
{
    private static readonly Uri Ok = new("https://example.com/ok");
    private static readonly Uri Missing = new("https://example.com/missing");
    private static readonly Uri NoHead = new("https://example.com/no-head");
    private static readonly Uri Down = new("https://down.example.com/");

    private static LinkChecker CreateChecker(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new CrawlerOptions()));

    [Fact]
    public async Task Returns_a_result_with_the_status_code_for_every_link()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(
            request.RequestUri == Missing ? HttpStatusCode.NotFound : HttpStatusCode.OK));
        var checker = CreateChecker(handler);

        var results = await checker.CheckAsync([Ok, Missing], CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(200, results.Single(r => r.Url == Ok).StatusCode);
        Assert.Equal(404, results.Single(r => r.Url == Missing).StatusCode);
    }

    [Fact]
    public async Task Uses_head_requests_first()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var checker = CreateChecker(handler);

        await checker.CheckAsync([Ok], CancellationToken.None);

        Assert.Equal(HttpMethod.Head, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task Falls_back_to_get_when_head_is_not_allowed()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(
            request.Method == HttpMethod.Head ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.OK));
        var checker = CreateChecker(handler);

        var results = await checker.CheckAsync([NoHead], CancellationToken.None);

        Assert.Equal(200, Assert.Single(results).StatusCode);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task Records_an_error_instead_of_throwing_when_a_request_fails()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri == Down
            ? throw new HttpRequestException("No such host is known.")
            : new HttpResponseMessage(HttpStatusCode.OK));
        var checker = CreateChecker(handler);

        var results = await checker.CheckAsync([Ok, Down], CancellationToken.None);

        var failed = results.Single(r => r.Url == Down);
        Assert.Null(failed.StatusCode);
        Assert.NotNull(failed.Error);
        Assert.True(failed.IsBroken);
        Assert.Equal(200, results.Single(r => r.Url == Ok).StatusCode);
    }
}
