using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SiteHealth.Api.Scans;

/// <summary>Rate limit settings. Bound from the "RateLimit" section of appsettings.json.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>How many scans one IP address can start per minute.</summary>
    public int ScansPerMinute { get; set; } = 10;

    /// <summary>How many status checks (GET /scans/{id}) one IP address can make per minute. Polling is normal, so this is higher.</summary>
    public int ReadsPerMinute { get; set; } = 120;
}

/// <summary>
/// Limits how often each caller can use the API, with ASP.NET Core's built-in rate limiter.
/// The queue limit already protects memory, but without this one caller could fill the whole queue
/// and lock everyone else out.
/// </summary>
/// <remarks>
/// Limits are per IP address. Two things to know for later:
/// <list type="bullet">
/// <item>Behind Azure's front end every request seems to come from Azure's own address. The deploy step must
/// turn on forwarded headers (X-Forwarded-For) so RemoteIpAddress is the real caller again.</item>
/// <item>The counters live in each app instance's memory, so with 3 instances a caller gets 3 times the limit.
/// Once there's sign-in, we'll limit per user instead.</item>
/// </list>
/// </remarks>
public static class ScanRateLimits
{
    public const string CreateScan = "create-scan";
    public const string ReadScan = "read-scan";

    public static IServiceCollection AddScanRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(CreateScan, context => PerIpPerMinute(context, options => options.ScansPerMinute));
            limiter.AddPolicy(ReadScan, context => PerIpPerMinute(context, options => options.ReadsPerMinute));

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    // A "fixed window": each IP address gets N requests per one-minute window; the count resets every minute.
    // Each IP address has its own counter: that's what the partition key does.
    private static RateLimitPartition<string> PerIpPerMinute(HttpContext context, Func<RateLimitOptions, int> limit)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit(options),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0, // over the limit means "no" straight away, not "wait in line"
        });
    }

    // 429 with a Retry-After header (seconds until the window resets) and the usual ProblemDetails body
    private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests. Please wait a moment and try again.",
            },
        });
    }
}
