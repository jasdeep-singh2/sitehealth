using SiteHealth.Core;

namespace SiteHealth.Api.Scans;

// The JSON shapes the API sends and receives. They're separate from the Core types on purpose:
// Core can change shape freely, while these stay stable for whoever calls the API (the React app later).

/// <summary>Body of POST /scans.</summary>
public sealed record CreateScanRequest(string? Url);

/// <summary>What GET /scans/{id} returns. <see cref="Result"/> is only set once the scan has completed.</summary>
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

/// <param name="LinksSkipped">Links found but not checked, because the page had more than the per-page limit.</param>
public sealed record ScanResult(int LinksChecked, int LinksSkipped, int BrokenCount, IReadOnlyList<BrokenLinkGroup> Groups)
{
    public static ScanResult From(ScanReport report) => new(
        report.LinksChecked,
        report.LinksSkipped,
        report.BrokenCount,
        report.Groups.Select(group => BrokenLinkGroup.From(group, report.AdviceByGroup.GetValueOrDefault(group.Id))).ToList());
}

/// <param name="Advice">Claude's explanation for the group, or null when AI is off or failed.</param>
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

public sealed record Advice(string Severity, string Cause, string Fix);

public sealed record BrokenLink(string Url, int? StatusCode, string? Error);
