using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using SiteHealth.Api.Hosting;
using SiteHealth.Core;

namespace SiteHealth.Api.Tests;

public class TelemetryTests
{
    [Fact]
    public void Recognises_the_crawlers_requests_by_their_user_agent()
    {
        var userAgent = new CrawlerOptions().UserAgent;
        using var crawl = new HttpRequestMessage(HttpMethod.Head, "https://example.com/");
        crawl.Headers.UserAgent.ParseAdd(userAgent);
        using var claude = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        claude.Headers.UserAgent.ParseAdd("Anthropic/CSharp 13.3.0");

        Assert.True(Telemetry.IsCrawlerRequest(crawl, userAgent));
        Assert.False(Telemetry.IsCrawlerRequest(claude, userAgent));
    }

    // AddTelemetry decides while services are registered (in Azure the setting is an environment variable,
    // present from the start), so these tests call it directly rather than through the test web host.
    private static ServiceProvider Services(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Telemetry.ConnectionStringSetting] = connectionString })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddTelemetry(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public void Is_off_without_a_connection_string()
    {
        using var services = Services(connectionString: null);

        Assert.Null(services.GetService<TracerProvider>());
    }

    [Fact]
    public void Is_on_with_a_connection_string()
    {
        // Well-formed but unused: nothing is sent during the test
        using var services = Services("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost:1/");

        Assert.NotNull(services.GetService<TracerProvider>());
    }
}
