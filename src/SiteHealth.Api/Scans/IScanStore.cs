namespace SiteHealth.Api.Scans;

/// <summary>
/// Saves and loads scans. The app uses <see cref="Data.EfScanStore"/> (EF Core + SQL Server).
/// </summary>
public interface IScanStore
{
    Task AddAsync(Scan scan, CancellationToken cancellationToken);

    Task<Scan?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(Scan scan, CancellationToken cancellationToken);

    /// <summary>
    /// Scans that were Queued or Running, oldest first. After a restart these were interrupted:
    /// the in-memory queue is gone, so the worker picks them up from here instead.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUnfinishedIdsAsync(CancellationToken cancellationToken);
}
