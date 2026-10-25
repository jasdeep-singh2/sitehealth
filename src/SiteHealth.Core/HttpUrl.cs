using System.Diagnostics.CodeAnalysis;

namespace SiteHealth.Core;

/// <summary>
/// The one rule for "is this a URL we can scan?", shared by the CLI and the API.
/// </summary>
public static class HttpUrl
{
    /// <summary>Accepts only absolute http:// or https:// URLs.</summary>
    public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out Uri? url)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            return true;

        url = null;
        return false;
    }
}
