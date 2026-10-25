using System.Collections.Concurrent;

namespace SiteHealth.Api.Scans;

/// <summary>
/// Keeps scans in memory. Good enough to build and test the API, but everything is lost
/// when the app restarts. The next step replaces it with EF Core.
/// </summary>
/// <remarks>
/// ConcurrentDictionary because the API (many requests at once) and the worker read and write at the same time.
/// </remarks>
public sealed class InMemoryScanStore : IScanStore
{
    private readonly ConcurrentDictionary<Guid, Scan> _scans = new();

    public Task AddAsync(Scan scan, CancellationToken cancellationToken)
    {
        if (!_scans.TryAdd(scan.Id, scan))
            throw new InvalidOperationException($"Scan {scan.Id} already exists.");

        return Task.CompletedTask;
    }

    public Task<Scan?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_scans.GetValueOrDefault(id));

    public Task UpdateAsync(Scan scan, CancellationToken cancellationToken)
    {
        _scans[scan.Id] = scan;
        return Task.CompletedTask;
    }
}
