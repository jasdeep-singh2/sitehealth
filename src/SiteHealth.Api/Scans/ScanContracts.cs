using SiteHealth.Core;

namespace SiteHealth.Api.Scans;

// The JSON shapes the API sends and receives. They're separate from the Core types on purpose:
// Core can change shape freely, while these stay stable for whoever calls the API (the React app later).

/// <summary>Body of POST /scans.</summary>
/// <param name="Url">The public web page to scan: an absolute http or https URL, for example https://example.com.</param>
public sealed record CreateScanRequest(string? Url);

/// <summary>What GET /scans/{id} returns. <see cref="Result"/> is only set once the scan has completed.</summary>
/// <param name="Id">The scan's ID, used in GET /scans/{id}.</param>
/// <param name="Url">The page being scanned.</param>
/// <param name="Status">Queued, then Running, then Completed or Failed.</param>
/// <param name="CreatedAt">When the scan was requested.</param>
/// <param name="CompletedAt">When the scan finished, successfully or not. Null while it's Queued or Running.</param>
/// <param name="Error">Why the scan failed. Only set when the status is Failed.</param>
/// <param name="Result">What the scan found. Only set when the status is Completed.</param>
public sealed record ScanResponse(
    Guid Id,
    string Url,
    ScanStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    ScanResult? Result)
{
    public static ScanResponse From(Scan scan) => new(
        scan.Id,
        scan.Url.ToString(),
        scan.Status,
        scan.CreatedAt,
        scan.CompletedAt,
        scan.Error,
        scan.Report is null ? null : ScanResult.From(scan.Report));
}

/// <summary>What a completed scan found.</summary>
/// <param name="LinksChecked">How many links on the page were checked.</param>
/// <param name="LinksSkipped">Links found but not checked, because the page had more than the per-page limit.</param>
/// <param name="BrokenCount">How many of the checked links are broken.</param>
/// <param name="Groups">The broken links, grouped by problem: same-site groups first, then the biggest.</param>
public sealed record ScanResult(int LinksChecked, int LinksSkipped, int BrokenCount, IReadOnlyList<BrokenLinkGroup> Groups)
{
    public static ScanResult From(ScanReport report) => new(
        report.LinksChecked,
        report.LinksSkipped,
        report.BrokenCount,
        report.Groups.Select(group => BrokenLinkGroup.From(group, report.AdviceByGroup.GetValueOrDefault(group.Id))).ToList());
}

/// <summary>Broken links that share the same problem.</summary>
/// <param name="Id">1, 2, 3... in display order.</param>
/// <param name="IsSameSite">True when the links point at the same site as the scanned page.</param>
/// <param name="Problem">What went wrong: "status 404", "timed out" or "no response".</param>
/// <param name="Advice">Claude's explanation for the group, or null when AI is off or failed.</param>
/// <param name="Links">The broken links in this group, sorted by URL.</param>
public sealed record BrokenLinkGroup(
    int Id,
    bool IsSameSite,
    string Problem,
    Advice? Advice,
    IReadOnlyList<BrokenLink> Links)
{
    public static BrokenLinkGroup From(LinkGroup group, LinkAdvice? advice) => new(
        group.Id,
        group.IsSameSite,
        group.Problem,
        advice is null ? null : new Advice(advice.Severity, advice.Cause, advice.Fix),
        group.Links.Select(link => new BrokenLink(link.Url.ToString(), link.StatusCode, link.Error)).ToList());
}

/// <summary>Claude's explanation for a group of broken links.</summary>
/// <param name="Severity">"high", "medium" or "low".</param>
/// <param name="Cause">The most likely reason the links are broken.</param>
/// <param name="Fix">One practical way to fix them.</param>
public sealed record Advice(string Severity, string Cause, string Fix);

/// <summary>One broken link.</summary>
/// <param name="Url">Where the link points.</param>
/// <param name="StatusCode">The HTTP status the link returned, or null when there was no response.</param>
/// <param name="Error">Why there was no response (for example a timeout). Null when there was a status code.</param>
public sealed record BrokenLink(string Url, int? StatusCode, string? Error);
