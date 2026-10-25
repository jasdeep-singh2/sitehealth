using System.Net;
using System.Net.Http.Json;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

public class DashboardCorsTests
{
    private const string Dashboard = "https://dashboard.example.com";

    private static HttpResponseMessage Ok(HttpRequestMessage _) =>
        new(HttpStatusCode.OK) { Content = new StringContent("<html><body></body></html>") };

    private static SiteHealthApiFactory Api(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = Dashboard };
        foreach (var (key, value) in extra ?? []) settings[key] = value;
        return new SiteHealthApiFactory(Ok, settings: settings);
    }

    // What a browser sends before a cross-site POST with a JSON body: "may I?"
    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/scans");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }

    [Fact]
    public async Task Allows_the_dashboard_to_post_scans()
    {
        await using var factory = Api();
        var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight(Dashboard));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Dashboard, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }

    [Fact]
    public async Task Does_not_allow_other_sites()
    {
        await using var factory = Api();
        var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Lets_the_dashboard_read_the_location_header()
    {
        await using var factory = Api();
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/scans") { Content = JsonContent.Create(new CreateScanRequest("https://example.com/")) };
        request.Headers.Add("Origin", Dashboard);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(Dashboard, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("Location", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task Rate_limited_answers_are_still_readable_by_the_dashboard()
    {
        await using var factory = Api(new() { ["RateLimit:ScansPerMinute"] = "1" });
        var client = factory.CreateClient();

        HttpResponseMessage last = null!;
        for (var i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/scans") { Content = JsonContent.Create(new CreateScanRequest("https://example.com/")) };
            request.Headers.Add("Origin", Dashboard);
            last = await client.SendAsync(request);
        }

        // Without CORS headers the browser would hide the 429, and the dashboard couldn't say "wait 60 seconds"
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Equal(Dashboard, last.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
