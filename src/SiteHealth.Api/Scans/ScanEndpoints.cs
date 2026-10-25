using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SiteHealth.Core;
using SiteHealth.Core.Security;

namespace SiteHealth.Api.Scans;

/// <summary>
/// POST /scans starts a scan and answers straight away; GET /scans/{id} reports how it's going.
/// This is the asynchronous request-reply pattern: the slow work happens in <see cref="ScanWorker"/>,
/// so no HTTP request ever waits for a whole scan.
/// </summary>
public static class ScanEndpoints
{
    public static IEndpointRouteBuilder MapScanEndpoints(this IEndpointRouteBuilder app)
    {
        var scans = app.MapGroup("/scans").WithTags("Scans");

        // WithName becomes the OpenAPI "operationId": a stable name the generated web client can use.
        // ProducesProblem adds the error responses that the Results<...> types below can't show:
        // ProblemHttpResult could be any status, and 429 comes from the rate limiter, not the endpoint.
        scans.MapPost("", CreateScanAsync)
            .WithName("CreateScan")
            .WithSummary("Start a scan")
            .WithDescription("Queues a scan of one public web page and answers straight away with 202 Accepted. "
                + "Follow the Location header (GET /scans/{id}) to see how it's going.")
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(ScanRateLimits.CreateScan);

        scans.MapGet("/{id:guid}", GetScanAsync)
            .WithName("GetScan")
            .WithSummary("Check on a scan")
            .WithDescription("Returns the scan's status. Once it's Completed, the result holds the broken links, "
                + "grouped by problem, with AI advice when it's switched on. Poll every few seconds until "
                + "the status is Completed or Failed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(ScanRateLimits.ReadScan);

        return app;
    }

    // The Results<...> return type lists every response this endpoint can give. The compiler checks
    // we only return those, and OpenAPI reads the success and validation responses from it.
    private static async Task<Results<Accepted<ScanResponse>, ValidationProblem, ProblemHttpResult>> CreateScanAsync(
        CreateScanRequest request,
        IScanStore store,
        IScanQueue queue,
        TimeProvider time,
        IOptions<CrawlerOptions> crawlerOptions,
        CancellationToken cancellationToken)
    {
        if (!HttpUrl.TryParse(request.Url, out var url))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = ["Enter an absolute http or https URL, for example https://example.com."],
            });
        }

        // A quick, friendly "no" for obvious cases like http://localhost or http://10.0.0.1.
        // The real protection is PublicNetworkConnector, which also catches redirects and tricky DNS.
        if (!crawlerOptions.Value.AllowPrivateNetworks && PublicAddressPolicy.IsObviouslyPrivate(url))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = ["SiteHealth can only scan public websites. Local, private and internal addresses aren't allowed."],
            });
        }

        var scan = new Scan(Guid.CreateVersion7(), url, ScanStatus.Queued, time.GetUtcNow());

        // Save first, then queue: the worker looks the scan up by ID, so it must exist before the worker sees it
        await store.AddAsync(scan, cancellationToken);

        if (!queue.TryEnqueue(scan.Id))
        {
            await store.UpdateAsync(
                scan with { Status = ScanStatus.Failed, Error = "Too many scans are queued.", CompletedAt = time.GetUtcNow() },
                cancellationToken);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Too many scans are queued. Try again in a minute.");
        }

        // 202 Accepted: "got it, working on it". The Location header tells the caller where to check.
        return TypedResults.Accepted($"/scans/{scan.Id}", ScanResponse.From(scan));
    }

    private static async Task<Results<Ok<ScanResponse>, ProblemHttpResult>> GetScanAsync(
        Guid id,
        IScanStore store,
        CancellationToken cancellationToken)
    {
        var scan = await store.GetAsync(id, cancellationToken);

        return scan is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Scan not found.")
            : TypedResults.Ok(ScanResponse.From(scan));
    }
}
