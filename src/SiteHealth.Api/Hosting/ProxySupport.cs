using Microsoft.AspNetCore.HttpOverrides;

namespace SiteHealth.Api.Hosting;

/// <summary>Settings for running behind a reverse proxy. Bound from the "Proxy" section.</summary>
public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    /// <summary>
    /// Trust the X-Forwarded-For and X-Forwarded-Proto headers from the proxy in front of the app.
    /// Switch it on only when the app can't be reached any other way (true for Azure Container Apps,
    /// whose ingress is the only way in). Otherwise anyone could send the header and pretend to be someone else.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }
}

/// <summary>
/// Behind Azure's front end, every request reaches the app from Azure's proxy, so RemoteIpAddress is the
/// proxy's address and every caller would share one rate limit. The proxy adds the caller's real address
/// in X-Forwarded-For; this reads it back into RemoteIpAddress (and https into Request.Scheme).
/// </summary>
public static class ProxySupport
{
    public static IServiceCollection AddProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProxyOptions>(configuration.GetSection(ProxyOptions.SectionName));
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Only read the last entry: the one Azure's proxy added. A caller can put anything in the header,
            // but anything they send ends up further left, so it's ignored.
            options.ForwardLimit = 1;

            // By default only proxies on localhost are trusted. Azure's proxy addresses aren't fixed, so we
            // trust whoever is directly in front of us. That's safe only because ProxyOptions says the proxy
            // is the only way in.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>Must run before anything that looks at the caller's IP, such as the rate limiter.</summary>
    public static IApplicationBuilder UseProxySupport(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProxyOptions>>().Value;
        return options.TrustForwardedHeaders ? app.UseForwardedHeaders() : app;
    }
}
