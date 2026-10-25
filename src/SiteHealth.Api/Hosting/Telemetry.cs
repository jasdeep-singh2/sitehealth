using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using SiteHealth.Core;

namespace SiteHealth.Api.Hosting;

/// <summary>
/// Sends logs, request timings and errors to Azure Application Insights, using OpenTelemetry (the vendor-neutral
/// standard for telemetry) and Microsoft's Azure Monitor package. Switched on only when Azure provides
/// APPLICATIONINSIGHTS_CONNECTION_STRING, so local runs and tests send nothing anywhere.
/// </summary>
public static class Telemetry
{
    public const string ConnectionStringSetting = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[ConnectionStringSetting]))
            return services;

        // Records incoming requests, outgoing HTTP calls (such as to Claude), and ILogger logs.
        // The connection string is read from configuration; it's never in code.
        services.AddOpenTelemetry().UseAzureMonitor();

        // But not the crawl itself: a scan can check 500 links, and a record of each would drown out
        // everything else and cost money. The worker's logs already say what each scan found.
        services.AddOptions<HttpClientTraceInstrumentationOptions>()
            .Configure<IOptions<CrawlerOptions>>((tracing, crawler) =>
                tracing.FilterHttpRequestMessage = request => !IsCrawlerRequest(request, crawler.Value.UserAgent));

        return services;
    }

    /// <summary>True for requests to the sites being scanned, which identify themselves with the crawler's User-Agent.</summary>
    public static bool IsCrawlerRequest(HttpRequestMessage request, string crawlerUserAgent) =>
        string.Equals(request.Headers.UserAgent.ToString(), crawlerUserAgent, StringComparison.Ordinal);
}
