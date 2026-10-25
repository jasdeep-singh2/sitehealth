namespace SiteHealth.Cli;

/// <summary>
/// Broken links that share the same problem, for example "status 404 on this site".
/// Claude explains each group once, instead of repeating the same advice for every link.
/// </summary>
/// <param name="Id">1, 2, 3... Claude refers to the group by this number.</param>
/// <param name="IsSameSite">True when the links point at the same site as the page that was checked.</param>
/// <param name="Problem">What went wrong: "status 404", "timed out" or "no response".</param>
/// <param name="Links">The broken links in this group, sorted by URL.</param>
public sealed record LinkGroup(int Id, bool IsSameSite, string Problem, IReadOnlyList<LinkCheckResult> Links);
