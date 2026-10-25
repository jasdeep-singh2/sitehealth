using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace SiteHealth.Core;

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
    // Tests that define "done": tests/SiteHealth.Core.Tests/LinkCheckerTests.cs
    public async Task<IReadOnlyList<LinkCheckResult>> CheckAsync(IEnumerable<Uri> links, CancellationToken cancellationToken)
    {
        // A thread-safe bag: 5 workers can add results at the same time without clashing
        var results = new ConcurrentBag<LinkCheckResult>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrency,          // Blank 1: how many at once? (it's in our settings)
            CancellationToken = cancellationToken,
        };

        // Check the links in parallel, a few at a time
        await Parallel.ForEachAsync(links, parallelOptions, async (url, token) =>
        {
            results.Add(await CheckOneAsync(url, token));
        });

        return results.ToList();
    }

    private async Task<LinkCheckResult> CheckOneAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            // Rule 2: ask with HEAD first (no page download)
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);   // Blank 2: which HTTP method?
            using var response = await httpClient.SendAsync(headRequest, cancellationToken);

            // Rule 3: if the server says "HEAD not allowed", try GET instead
            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)                              // Blank 3: the 405 status
            {
                using var getResponse = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return new LinkCheckResult(url, (int)getResponse.StatusCode, null);
            }

            // Rule 1: record the status code (200, 404, ...)
            return new LinkCheckResult(url, (int)response.StatusCode, null);
        }
        catch (HttpRequestException ex)
        {
            // Rule 4: the site is down or unreachable. Record it, don't crash.
            // No response means no status code
            return new LinkCheckResult(url, null, ConnectionErrors.Describe(ex));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The site took too long (timeout). Record it, don't crash.
            return new LinkCheckResult(url, null, "Timed out");
        }
    }
}
