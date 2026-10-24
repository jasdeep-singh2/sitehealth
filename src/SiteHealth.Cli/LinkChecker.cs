using Microsoft.Extensions.Options;

namespace SiteHealth.Cli;

/// <summary>
/// Checks whether each link responds, in parallel but with a concurrency limit.
/// </summary>
public sealed class LinkChecker(HttpClient httpClient, IOptions<CrawlerOptions> options)
{
    private readonly CrawlerOptions _options = options.Value;

    // STEP 3: you write this.
    //
    // Goal: return one LinkCheckResult per link. Never throw because a single link failed.
    //  - Run checks in parallel with Parallel.ForEachAsync, limited by
    //    new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrency, CancellationToken = cancellationToken }
    //  - Collect results in a thread-safe collection (e.g. ConcurrentBag<LinkCheckResult>).
    //  - For each link, send a HEAD request first: it's cheap because no body is downloaded.
    //  - Some servers don't support HEAD and answer 405 Method Not Allowed: retry those with GET.
    //  - Record the status code, e.g. new LinkCheckResult(url, (int)response.StatusCode, null)
    //  - If the request throws HttpRequestException or times out (TaskCanceledException),
    //    record new LinkCheckResult(url, null, ex.Message) instead of crashing.
    //    Careful: if cancellationToken itself was cancelled (Ctrl+C), let that exception bubble up.
    //
    // Why this matters for system design: limiting concurrency is "back-pressure". It protects the
    // site we're checking and our own machine. Later, Service Bus and scaled-out workers do the same job.
    //
    // Tests that define "done": tests/SiteHealth.Cli.Tests/LinkCheckerTests.cs
    public Task<IReadOnlyList<LinkCheckResult>> CheckAsync(IEnumerable<Uri> links, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Step 3: check each link's status code.");
    }
}
