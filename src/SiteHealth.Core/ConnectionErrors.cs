namespace SiteHealth.Core;

/// <summary>
/// Turns HttpClient's connection errors into messages a site owner can act on. Some of .NET's own
/// messages point at details users can't see, such as
/// "The SSL connection could not be established, see inner exception."
/// </summary>
public static class ConnectionErrors
{
    public static string Describe(HttpRequestException exception)
    {
        // HttpRequestError (added in .NET 8) says what kind of failure it was, without parsing messages
        if (exception.HttpRequestError == HttpRequestError.SecureConnectionError)
            return "Couldn't make a secure (HTTPS) connection: the site's certificate may be invalid, or the site may not support HTTPS";

        // "…see inner exception." means the useful part is one level down
        if (exception.InnerException is { } inner
            && exception.Message.Contains("see inner exception", StringComparison.OrdinalIgnoreCase))
            return inner.Message;

        return exception.Message;
    }
}
