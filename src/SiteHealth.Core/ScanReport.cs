namespace SiteHealth.Core;

/// <summary>
/// Everything one scan of a page found. The CLI prints it; the API worker will store it.
/// </summary>
/// <param name="PageUrl">The page that was scanned.</param>
/// <param name="FetchError">Set when the page itself couldn't be fetched. Nothing else ran, so the rest is empty.</param>
/// <param name="LinksChecked">How many links were found on the page and checked.</param>
/// <param name="BrokenCount">How many of those links are broken.</param>
/// <param name="Groups">The broken links, grouped by problem (see <see cref="LinkGrouper"/>).</param>
/// <param name="AdviceByGroup">Claude's advice, keyed by <see cref="LinkGroup.Id"/>. Empty when AI is off or failed.</param>
public sealed record ScanReport(
    Uri PageUrl,
    string? FetchError,
    int LinksChecked,
    int BrokenCount,
    IReadOnlyList<LinkGroup> Groups,
    IReadOnlyDictionary<int, LinkAdvice> AdviceByGroup)
{
    public static ScanReport FetchFailed(Uri pageUrl, string error) =>
        new(pageUrl, error, 0, 0, [], new Dictionary<int, LinkAdvice>());
}
