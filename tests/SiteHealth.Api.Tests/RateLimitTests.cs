using System.Net;
using System.Net.Http.Json;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

public class RateLimitTests
{
    // Every page and link answers 200, so scans are quick and boring: these tests are about the limits
    private static HttpResponseMessage Ok(HttpRequestMessage _) =>
        new(HttpStatusCode.OK) { Content = new StringContent("<html><body></body></html>") };

    [Fact]
    public async Task Post_returns_429_with_retry_after_once_the_limit_is_used_up()
    {
        await using var factory = new SiteHealthApiFactory(Ok, settings: new Dictionary<string, string?>
        {
            ["RateLimit:ScansPerMinute"] = "2",
        });
        var client = factory.CreateClient();
        var request = new CreateScanRequest("https://example.com/");

        var first = await client.PostAsJsonAsync("/scans", request);
        var second = await client.PostAsJsonAsync("/scans", request);
        var third = await client.PostAsJsonAsync("/scans", request);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType!.MediaType);

        var retryAfter = int.Parse(Assert.Single(third.Headers.GetValues("Retry-After")));
        Assert.InRange(retryAfter, 1, 60);
    }

    [Fact]
    public async Task Rejected_posts_do_not_create_scans()
    {
        await using var factory = new SiteHealthApiFactory(Ok, settings: new Dictionary<string, string?>
        {
            ["RateLimit:ScansPerMinute"] = "1",
        });
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));
        var rejected = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));

        // The limiter answers before the endpoint runs, so nothing was saved or queued
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Null(rejected.Headers.Location);
    }

    [Fact]
    public async Task Status_checks_have_their_own_separate_limit()
    {
        await using var factory = new SiteHealthApiFactory(Ok, settings: new Dictionary<string, string?>
        {
            ["RateLimit:ScansPerMinute"] = "1",
            ["RateLimit:ReadsPerMinute"] = "3",
        });
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));
        var location = created.Headers.Location!;

        // The scan limit (1) is used up, but checking on the scan still works: GETs count separately
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.GetAsync(location)).StatusCode);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }
}
