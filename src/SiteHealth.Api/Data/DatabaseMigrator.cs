using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SiteHealth.Api.Data;

/// <summary>
/// Applies EF Core migrations when the app starts, if <see cref="DatabaseOptions.MigrateOnStartup"/> is on.
/// </summary>
/// <remarks>
/// A hosted service rather than code in Program.cs, so it can be left out when the build starts the app to
/// write the OpenAPI document (see <see cref="BuildTimeDocument"/>): a build must never need a database.
/// Hosted services start in the order they're registered, so registering this before
/// <see cref="Scans.ScanWorker"/> means the tables exist before the worker looks for unfinished scans.
/// </remarks>
public sealed class DatabaseMigrator(
    IOptions<DatabaseOptions> options,
    IDbContextFactory<SiteHealthDbContext> contextFactory,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.MigrateOnStartup)
            return;

        logger.LogInformation("Applying database migrations");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
