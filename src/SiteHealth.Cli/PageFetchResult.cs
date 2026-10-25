using System.Diagnostics.CodeAnalysis;

namespace SiteHealth.Cli;

/// <summary>
/// The outcome of fetching one page. A failed fetch is a normal result, not an exception:
/// sites go down all the time, and callers (the CLI now, the worker later) need to report it.
/// <paramref name="Html"/> is set only on success. <paramref name="StatusCode"/> is null when
/// no response came back (timeout, DNS failure, refused connection); <paramref name="Error"/> says why.
/// </summary>
public sealed record PageFetchResult(Uri Url, string? Html, int? StatusCode, string? Error)
{
    // Tells the nullable checker that Html is not null once IsSuccess is true, so callers need no `!`.
    [MemberNotNullWhen(true, nameof(Html))]
    public bool IsSuccess => Html is not null;
}
