namespace SiteHealth.Api.Scans;

/// <summary>
/// Hands scan IDs from the API to the worker. Today it's an in-memory channel; in Phase 2 it
/// becomes Azure Service Bus, and the worker can move to its own app without other code changing.
/// </summary>
public interface IScanQueue
{
    /// <summary>Adds a scan to the queue. Returns false when the queue is full.</summary>
    bool TryEnqueue(Guid scanId);

    /// <summary>Waits for scans and yields them one by one until <paramref name="cancellationToken"/> fires.</summary>
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
