using SiteHealth.Core;

namespace SiteHealth.Api.Scans;

/// <summary>Where a scan is in its life: Queued → Running → Completed or Failed.</summary>
public enum ScanStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

/// <summary>
/// One scan request and, once the worker has run it, its result.
/// It's an immutable record: the worker makes a changed copy with <c>with</c> and saves that,
/// so a half-updated scan can never be seen by the API.
/// </summary>
/// <param name="Id">
/// A version 7 GUID: unique, and its text sorts by creation time. (SQL Server compares GUIDs by their
/// last bytes first, so this doesn't make SQL Server's index sequential; that only matters at large scale.)
/// </param>
/// <param name="Error">Why the scan failed. Only set when <paramref name="Status"/> is Failed.</param>
/// <param name="Report">What the scan found. Only set when <paramref name="Status"/> is Completed.</param>
public sealed record Scan(
    Guid Id,
    Uri Url,
    ScanStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null,
    string? Error = null,
    ScanReport? Report = null);
