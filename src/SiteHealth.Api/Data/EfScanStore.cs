using Microsoft.EntityFrameworkCore;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Data;

/// <summary>
/// Saves scans to the database with EF Core, so they survive a restart.
/// </summary>
/// <remarks>
/// A DbContext is meant to be short-lived and isn't thread-safe, but this store is a singleton used by
/// the API and the worker at the same time. So each method creates its own DbContext from the factory,
/// uses it for one job and disposes it. Database connections are pooled, so this is cheap.
/// </remarks>
public sealed class EfScanStore(IDbContextFactory<SiteHealthDbContext> contextFactory) : IScanStore
{
    public async Task AddAsync(Scan scan, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Scans.Add(scan);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Scan?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // AsNoTracking: we only read, so EF doesn't need to watch the object for changes
        return await db.Scans.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Scan scan, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Scan is an immutable record, so the caller hands us a new copy. Update() saves all its columns.
        db.Scans.Update(scan);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetUnfinishedIdsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var unfinished = await db.Scans
            .AsNoTracking()
            .Where(s => s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running)
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync(cancellationToken);

        // Oldest first. Sorted here rather than in SQL because SQLite (used in tests) can't sort
        // DateTimeOffset columns, and this list is small.
        return unfinished.OrderBy(s => s.CreatedAt).Select(s => s.Id).ToList();
    }
}
