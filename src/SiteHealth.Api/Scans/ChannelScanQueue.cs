using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace SiteHealth.Api.Scans;

/// <summary>
/// An in-memory queue built on <see cref="Channel{T}"/>: .NET's thread-safe "producer puts in,
/// consumer takes out" pipe. The API is the producer, <see cref="ScanWorker"/> the consumer.
/// </summary>
/// <remarks>
/// The channel is bounded: once it holds <see cref="ScanQueueOptions.Capacity"/> scans, new ones are
/// refused instead of piling up in memory forever. That's back-pressure, the same idea as the
/// LinkChecker's concurrency limit. Queued scans are lost if the app restarts; Service Bus fixes that later.
/// </remarks>
public sealed class ChannelScanQueue(IOptions<ScanQueueOptions> options) : IScanQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(options.Value.Capacity) { SingleReader = true });

    public bool TryEnqueue(Guid scanId) => _channel.Writer.TryWrite(scanId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
