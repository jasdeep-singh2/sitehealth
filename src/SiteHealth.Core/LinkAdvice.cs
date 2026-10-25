namespace SiteHealth.Core;

/// <summary>
/// Claude's explanation for one group of broken links (see <see cref="LinkGroup"/>): why they're
/// probably broken, how to fix them, and how urgent it is ("high", "medium" or "low").
/// </summary>
public sealed record LinkAdvice(int GroupId, string Cause, string Fix, string Severity);
