using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SiteHealth.Api.Scans;

public static class ScanServiceCollectionExtensions
{
    /// <summary>Registers the scan queue, store and background worker.</summary>
    public static IServiceCollection AddScans(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScanQueueOptions>(configuration.GetSection(ScanQueueOptions.SectionName));

        // Singletons: there is one queue and one store for the whole app, shared by the API and the worker
        services.AddSingleton<IScanQueue, ChannelScanQueue>();
        services.AddSingleton<IScanStore, InMemoryScanStore>();

        // TimeProvider instead of DateTimeOffset.UtcNow, so tests can control the clock if they need to
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<ScanWorker>();
        return services;
    }
}
