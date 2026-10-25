namespace SiteHealth.Core;

/// <summary>
/// Sorts broken links into groups that share the same problem.
/// This is plain code on purpose: grouping is free, gives the same answer every time and is easy
/// to test. We save Claude for the part only it can do, explaining the problem.
/// </summary>
public static class LinkGrouper
{
    // Tests that define "done": tests/SiteHealth.Core.Tests/LinkGrouperTests.cs
    public static IReadOnlyList<LinkGroup> Group(Uri pageUrl, IEnumerable<LinkCheckResult> brokenLinks)
    {
        return brokenLinks
            // Step 1: put links with the same (site, problem) together.
            // The key is a tuple, so two links land in the same group only if BOTH parts match.
            .GroupBy(link => (
                IsSameSite: IsSameSite(pageUrl, link),
                Problem: DescribeProblem(link)))

            // Step 2: same-site groups first (they matter most), then the biggest groups first.
            // Descending puts true before false, and big numbers before small ones.
            .OrderByDescending(group => group.Key.IsSameSite)   // Blank 4
            .ThenByDescending(group => group.Count())           // Blank 5
            // Tie-break so equal-sized groups always come out in the same order. Without it the order
            // follows LinkChecker's parallel results, which change from run to run.
            .ThenBy(group => group.Key.Problem, StringComparer.Ordinal)

            // Step 3: number the groups and sort each group's links by URL.
            // This Select overload hands you each item's position: 0, 1, 2...
            .Select((group, index) => new LinkGroup(
                Id: index + 1,                           // Blank 6
                IsSameSite: group.Key.IsSameSite,
                Problem: group.Key.Problem,
                Links: group.OrderBy(link => link.Url.ToString()).ToList()))
            .ToList();
    }

    // Is the link on the same site as the page we checked?
    // Uri already lowercases the host (new Uri("https://EXAMPLE.com").Host is "example.com"),
    // so comparing the two Host values with == is enough.
    private static bool IsSameSite(Uri pageUrl, LinkCheckResult link) =>
        link.Url.Host == pageUrl.Host;                  // Blank 1

    // Turns a link's result into a short label that Claude and the console output both use.
    private static string DescribeProblem(LinkCheckResult link)
    {
        // The server answered with an error status, e.g. 404
        if (link.StatusCode is int status)
            return $"status {status}";                  // Blank 2

        // No answer at all. LinkChecker records the text "Timed out" for a timeout.
        return link.Error == "Timed out"
            ? "timed out"                               // Blank 3a
            : "no response";                            // Blank 3b
    }
}
