using Microsoft.Extensions.Options;

namespace SiteHealth.Core;

/// <summary>
/// Runs one complete scan of a page: fetch it, extract its links, check them, group the broken
/// ones and ask Claude about each group. The CLI and the API worker both call this, so they can
/// never drift apart.
/// </summary>
/// <remarks>
/// <paramref name="advisor"/> is optional: it's only registered when an API key is available.
/// The default value tells dependency injection to pass null instead of failing.
/// </remarks>
public sealed class ScanRunner(
    PageFetcher fetcher,
    LinkExtractor extractor,
    LinkChecker checker,
    IOptions<CrawlerOptions> options,
    ILinkAdvisor? advisor = null)
{
    private readonly int _maxLinks = options.Value.MaxLinksPerPage;

    /// <summary>
    /// Scans <paramref name="pageUrl"/>. Status messages ("Fetching...", "Found 47 links...") go to
    /// <paramref name="progress"/>, so the CLI can print them and the worker can ignore or log them.
    /// </summary>
    public async Task<ScanReport> RunAsync(Uri pageUrl, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report($"Fetching {pageUrl} ...");
        var page = await fetcher.FetchAsync(pageUrl, cancellationToken);
        if (!page.IsSuccess)
            return ScanReport.FetchFailed(pageUrl, page.Error ?? "Unknown error");

        var links = extractor.ExtractLinks(page.Html, pageUrl);

        // Check at most MaxLinksPerPage links, in the order they appear on the page
        var skipped = Math.Max(0, links.Count - _maxLinks);
        progress?.Report(skipped == 0
            ? $"Found {links.Count} links. Checking them..."
            : $"Found {links.Count} links. Checking the first {_maxLinks} (the limit per page)...");

        var results = await checker.CheckAsync(links.Take(_maxLinks), cancellationToken);
        var broken = results.Where(r => r.IsBroken).ToList();
        var groups = LinkGrouper.Group(pageUrl, broken);

        var adviceByGroup = await GetAdviceAsync(pageUrl, groups, progress, cancellationToken);

        return new ScanReport(pageUrl, null, results.Count, broken.Count, groups, adviceByGroup, skipped);
    }

    // AI advice is a bonus: if it isn't configured or fails, we say so and still return the link results.
    private async Task<Dictionary<int, LinkAdvice>> GetAdviceAsync(
        Uri pageUrl, IReadOnlyList<LinkGroup> groups, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
            return [];

        if (advisor is null)
        {
            progress?.Report("Tip: set the ANTHROPIC_API_KEY environment variable to get AI explanations and fixes.");
            return [];
        }

        progress?.Report($"Asking Claude about {groups.Count} groups of broken links...");
        try
        {
            var advice = await advisor.AdviseAsync(pageUrl, groups, cancellationToken);
            return advice.ToDictionary(a => a.GroupId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report($"AI advice unavailable: {ex.Message}");
            return [];
        }
    }
}
