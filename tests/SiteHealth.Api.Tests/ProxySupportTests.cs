using System.Net;
using System.Net.Http.Json;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

/// <summary>
/// The rate limiter counts per caller IP, so it's a good way to see which IP the app thinks a request came from.
/// With a limit of 1 scan per minute: two different callers both get through, the same caller twice doesn't.
/// </summary>
public class ProxySupportTests
{
    private static HttpResponseMessage Ok(HttpRequestMessage _) =>
        new(HttpStatusCode.OK) { Content = new StringContent("<html><body></body></html>") };

    private static SiteHealthApiFactory Api(bool trustForwardedHeaders) => new(Ok, settings: new Dictionary<string, string?>
    {
        ["RateLimit:ScansPerMinute"] = "1",
        ["Proxy:TrustForwardedHeaders"] = trustForwardedHeaders ? "true" : "false",
    });

    private static Task<HttpResponseMessage> PostScanFrom(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/scans")
        {
            Content = JsonContent.Create(new CreateScanRequest("https://example.com/")),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Callers_behind_the_proxy_get_their_own_limits()
    {
        await using var factory = Api(trustForwardedHeaders: true);
        var client = factory.CreateClient();

        var alice = await PostScanFrom(client, "203.0.113.10");
        var bob = await PostScanFrom(client, "203.0.113.20");
        var aliceAgain = await PostScanFrom(client, "203.0.113.10");

        Assert.Equal(HttpStatusCode.Accepted, alice.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, bob.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, aliceAgain.StatusCode);
    }

    [Fact]
    public async Task Only_the_address_added_by_the_proxy_counts()
    {
        await using var factory = Api(trustForwardedHeaders: true);
        var client = factory.CreateClient();

        // A caller fakes an address at the start; the proxy appends the real one at the end
        var first = await PostScanFrom(client, "198.51.100.1, 203.0.113.10");
        var fakedAgain = await PostScanFrom(client, "198.51.100.2, 203.0.113.10");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fakedAgain.StatusCode);
    }

    [Fact]
    public async Task The_header_is_ignored_unless_switched_on()
    {
        await using var factory = Api(trustForwardedHeaders: false);
        var client = factory.CreateClient();

        // Different X-Forwarded-For values, but without trust they're the same caller
        var first = await PostScanFrom(client, "203.0.113.10");
        var second = await PostScanFrom(client, "203.0.113.20");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}
