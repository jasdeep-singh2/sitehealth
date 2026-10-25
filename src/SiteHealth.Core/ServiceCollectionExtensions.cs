using Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SiteHealth.Core.Security;

namespace SiteHealth.Core;

/// <summary>
/// Registers everything a scan needs in one call, so the CLI and the API set it up the same way.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSiteHealthCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CrawlerOptions>(configuration.GetSection(CrawlerOptions.SectionName));
        services.AddSingleton<LinkExtractor>();
        // Both clients reach websites through the SSRF-guarded handler (see CrawlerHttpHandler)
        services.AddHttpClient<PageFetcher>(ConfigureCrawlerClient).ConfigurePrimaryHttpMessageHandler(CreateCrawlerHandler);
        services.AddHttpClient<LinkChecker>(ConfigureCrawlerClient).ConfigurePrimaryHttpMessageHandler(CreateCrawlerHandler);
        services.AddTransient<ScanRunner>();

        // AI advice is optional: it's only switched on when an API key is available.
        // AnthropicClient reads ANTHROPIC_API_KEY itself, so the key never appears in our code or config.
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            services.AddSingleton(_ => new AnthropicClient());
            services.AddSingleton<ILinkAdvisor, ClaudeLinkAdvisor>();
        }

        return services;
    }

    private static void ConfigureCrawlerClient(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<CrawlerOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    }

    private static HttpMessageHandler CreateCrawlerHandler(IServiceProvider services) =>
        CrawlerHttpHandler.Create(services.GetRequiredService<IOptions<CrawlerOptions>>().Value);
}
