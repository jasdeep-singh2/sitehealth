namespace SiteHealth.Api.Scans;

/// <summary>
/// Saves and loads scans. The methods are async even though today's store is in memory,
/// because the next implementation (EF Core + SQL) will talk to a database.
/// Designing the interface for the real thing now means nothing else changes when we swap it.
/// </summary>
public interface IScanStore
{
    Task AddAsync(Scan scan, CancellationToken cancellationToken);

    Task<Scan?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(Scan scan, CancellationToken cancellationToken);
}
