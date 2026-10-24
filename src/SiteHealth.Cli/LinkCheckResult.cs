namespace SiteHealth.Cli;

/// <summary>
/// The outcome of checking one link. <paramref name="StatusCode"/> is null when the request
/// never got a response (timeout, DNS failure, refused connection); <paramref name="Error"/> says why.
/// </summary>
public sealed record LinkCheckResult(Uri Url, int? StatusCode, string? Error)
{
    public bool IsBroken => Error is not null || StatusCode is null or >= 400;
}
