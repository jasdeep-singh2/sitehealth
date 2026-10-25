using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SiteHealth.Api.Data;

namespace SiteHealth.Api.Scans;

public static class ScanServiceCollectionExtensions
{
    /// <summary>Registers the scan queue, the database-backed store and the background worker.</summary>
    public static IServiceCollection AddScans(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScanQueueOptions>(configuration.GetSection(ScanQueueOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionString = configuration.GetConnectionString("SiteHealth")
            ?? throw new InvalidOperationException("Connection string 'SiteHealth' is missing from configuration.");

        // A factory rather than AddDbContext: the store is a singleton, so it creates a short-lived
        // DbContext for each operation instead of holding one.
        services.AddDbContextFactory<SiteHealthDbContext>(options => options.UseSqlServer(connectionString));

        // Singletons: there is one queue and one store for the whole app, shared by the API and the worker
        services.AddSingleton<IScanQueue, ChannelScanQueue>();
        services.AddSingleton<IScanStore, EfScanStore>();

        // TimeProvider instead of DateTimeOffset.UtcNow, so tests can control the clock if they need to
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<ScanWorker>();
        return services;
    }
}
