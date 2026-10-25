using System.Net;

namespace SiteHealth.Api.Tests;

public class HealthEndpointsTests
{
    private static HttpResponseMessage NoWebsite(HttpRequestMessage _) => new(HttpStatusCode.NotFound);

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Reports_healthy(string path)
    {
        await using var factory = new SiteHealthApiFactory(NoWebsite);
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Is_never_rate_limited()
    {
        await using var factory = new SiteHealthApiFactory(NoWebsite, settings: new Dictionary<string, string?>
        {
            ["RateLimit:ReadsPerMinute"] = "1",
            ["RateLimit:ScansPerMinute"] = "1",
        });
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
