namespace SiteHealth.Core;

/// <summary>
/// Everything one scan of a page found. The CLI prints it; the API worker stores it.
/// </summary>
/// <param name="PageUrl">The page that was scanned.</param>
/// <param name="FetchError">Set when the page itself couldn't be fetched. Nothing else ran, so the rest is empty.</param>
/// <param name="LinksChecked">How many links were found on the page and checked.</param>
/// <param name="BrokenCount">How many of those links are broken.</param>
/// <param name="Groups">The broken links, grouped by problem (see <see cref="LinkGrouper"/>).</param>
/// <param name="AdviceByGroup">Claude's advice, keyed by <see cref="LinkGroup.Id"/>. Empty when AI is off or failed.</param>
/// <param name="LinksSkipped">
/// Links found but not checked, because the page had more than <see cref="CrawlerOptions.MaxLinksPerPage"/>.
/// Last, with a default of 0, so reports saved before this existed still read correctly: nothing was skipped then.
/// </param>
public sealed record ScanReport(
    Uri PageUrl,
    string? FetchError,
    int LinksChecked,
    int BrokenCount,
    IReadOnlyList<LinkGroup> Groups,
    IReadOnlyDictionary<int, LinkAdvice> AdviceByGroup,
    int LinksSkipped = 0)
{
    public static ScanReport FetchFailed(Uri pageUrl, string error) =>
        new(pageUrl, error, 0, 0, [], new Dictionary<int, LinkAdvice>());
}
