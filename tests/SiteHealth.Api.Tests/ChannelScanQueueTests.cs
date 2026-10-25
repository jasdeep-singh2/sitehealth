using Microsoft.Extensions.Options;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

public class ChannelScanQueueTests
{
    [Fact]
    public void Refuses_new_scans_when_full()
    {
        var queue = new ChannelScanQueue(Options.Create(new ScanQueueOptions { Capacity = 1 }));

        Assert.True(queue.TryEnqueue(Guid.NewGuid()));
        Assert.False(queue.TryEnqueue(Guid.NewGuid()));
    }

    [Fact]
    public async Task Hands_out_scans_in_the_order_they_were_queued()
    {
        var queue = new ChannelScanQueue(Options.Create(new ScanQueueOptions()));
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        queue.TryEnqueue(first);
        queue.TryEnqueue(second);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<Guid>();
        await foreach (var id in queue.DequeueAllAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == 2)
                break;
        }

        Assert.Equal([first, second], received);
    }
}
