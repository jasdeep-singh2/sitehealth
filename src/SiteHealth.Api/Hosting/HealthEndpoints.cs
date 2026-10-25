using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SiteHealth.Api.Data;

namespace SiteHealth.Api.Hosting;

/// <summary>
/// Two URLs Azure Container Apps calls to look after the app:
/// <list type="bullet">
/// <item><c>/health</c> (liveness): "is the process alive?" It checks nothing else, so a slow database never
/// makes Azure restart a healthy container.</item>
/// <item><c>/health/ready</c> (readiness): "can it do real work?" It also checks the database. While it fails,
/// Azure stops sending traffic but doesn't restart anything, which is right while a paused serverless
/// database wakes up.</item>
/// </list>
/// They aren't rate-limited (Azure calls them every few seconds) and aren't part of the OpenAPI document.
/// </summary>
public static class HealthEndpoints
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        services.AddHealthChecks()
            // Opens a connection with the app's own DbContext settings, so it tests exactly what the app uses
            .AddDbContextCheck<SiteHealthDbContext>("database", tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return app;
    }
}
