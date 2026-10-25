namespace SiteHealth.Cli;

/// <summary>
/// Claude's explanation for one broken link: why it's probably broken, how to fix it,
/// and how urgent it is ("high", "medium" or "low").
/// </summary>
public sealed record LinkAdvice(Uri Url, string Cause, string Fix, string Severity);
