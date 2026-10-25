namespace SiteHealth.Core.Security;

/// <summary>
/// Builds the HTTP handler that <see cref="PageFetcher"/> and <see cref="LinkChecker"/> use to reach websites.
/// </summary>
public static class CrawlerHttpHandler
{
    public static SocketsHttpHandler Create(CrawlerOptions options, PublicNetworkConnector? connector = null)
    {
        var handler = new SocketsHttpHandler
        {
            // Recreate connections now and then, so DNS changes are picked up (good practice for long-lived handlers)
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (!options.AllowPrivateNetworks)
        {
            handler.ConnectCallback = (connector ?? PublicNetworkConnector.Default).ConnectAsync;

            // With a proxy, we'd connect to the proxy and it would choose the real address, bypassing our
            // check. So the guarded handler always connects directly.
            handler.UseProxy = false;
        }

        return handler;
    }
}
