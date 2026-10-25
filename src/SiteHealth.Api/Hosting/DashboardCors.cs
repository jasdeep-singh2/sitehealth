using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace SiteHealth.Api.Hosting;

/// <summary>Which websites may call the API from a browser. Bound from the "Cors" section.</summary>
public sealed class DashboardCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Exact origins, such as "https://green-sea-0123.azurestaticapps.net". Empty means no other site may call
    /// the API from a browser, which is right for local development: Vite's proxy makes it the same site there.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>
/// CORS (cross-origin resource sharing). Browsers stop a page on one site from reading responses from
/// another unless that other site says it's allowed. In Azure the dashboard (Static Web Apps) and the API
/// (Container Apps) have different addresses, so the API has to name the dashboard's address.
/// </summary>
/// <remarks>
/// CORS only limits browsers; it isn't access control. Tools like curl ignore it, which is why the API has
/// its own protections (rate limits, the SSRF guard).
/// </remarks>
public static class DashboardCors
{
    private const string PolicyName = "dashboard";

    public static IServiceCollection AddDashboardCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DashboardCorsOptions>(configuration.GetSection(DashboardCorsOptions.SectionName));
        services.AddCors();

        // The policy is built from IOptions when it's first needed, not while services are being registered.
        // Settings that arrive late (test overrides, some hosting setups) are only complete by then.
        services.AddOptions<CorsOptions>().Configure<IOptions<DashboardCorsOptions>>((cors, dashboard) => cors.AddPolicy(PolicyName, policy => policy
            .WithOrigins(dashboard.Value.AllowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type")
            // Scripts can only read response headers the API lists here. The dashboard reads both.
            .WithExposedHeaders("Location", "Retry-After")
            // Browsers may remember a preflight answer for 10 minutes instead of asking before every POST
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    /// <summary>Must run before the rate limiter, so a refused request still carries the CORS headers the browser needs to read it.</summary>
    public static IApplicationBuilder UseDashboardCors(this IApplicationBuilder app) => app.UseCors(PolicyName);
}
