using SiteHealth.Core;

namespace SiteHealth.Api.Scans;

/// <summary>
/// Runs queued scans in the background, one at a time. A <see cref="BackgroundService"/> starts
/// with the app and runs until it shuts down, so slow scans never hold up an HTTP request.
/// </summary>
public sealed class ScanWorker(
    IScanQueue queue,
    IScanStore store,
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<ScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Waits quietly for the next scan; ends when the app shuts down (stoppingToken fires)
        await foreach (var scanId in queue.DequeueAllAsync(stoppingToken))
            await ProcessAsync(scanId, stoppingToken);
    }

    private async Task ProcessAsync(Guid scanId, CancellationToken stoppingToken)
    {
        var scan = await store.GetAsync(scanId, stoppingToken);
        if (scan is null)
            return;

        scan = scan with { Status = ScanStatus.Running };
        await store.UpdateAsync(scan, stoppingToken);

        try
        {
            // A worker is a singleton, but ScanRunner and its HttpClients are short-lived services.
            // A scope per scan gives us fresh ones and cleans them up afterwards.
            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ScanRunner>();

            var report = await runner.RunAsync(scan.Url, new LoggerProgress(logger, scanId), stoppingToken);

            scan = report.FetchError is null
                ? scan with { Status = ScanStatus.Completed, Report = report, CompletedAt = time.GetUtcNow() }
                : scan with { Status = ScanStatus.Failed, Error = report.FetchError, CompletedAt = time.GetUtcNow() };
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is shutting down: stop here rather than record a fake failure
            throw;
        }
        catch (Exception ex)
        {
            // Anything unexpected fails this one scan, not the worker. If the exception escaped,
            // ExecuteAsync would end and no further scans would ever run.
            logger.LogError(ex, "Scan {ScanId} of {Url} failed unexpectedly", scanId, scan.Url);
            scan = scan with { Status = ScanStatus.Failed, Error = "The scan failed unexpectedly.", CompletedAt = time.GetUtcNow() };
        }

        await store.UpdateAsync(scan, stoppingToken);
    }

    // Sends ScanRunner's status messages ("Found 47 links...") to the log, tagged with the scan ID.
    private sealed class LoggerProgress(ILogger logger, Guid scanId) : IProgress<string>
    {
        public void Report(string value) => logger.LogInformation("Scan {ScanId}: {Message}", scanId, value);
    }
}
