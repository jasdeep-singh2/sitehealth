using Microsoft.AspNetCore.Http.HttpResults;
using SiteHealth.Core;

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
        var scans = app.MapGroup("/scans");
        scans.MapPost("", CreateScanAsync);
        scans.MapGet("/{id:guid}", GetScanAsync);
        return app;
    }

    // The Results<...> return type lists every response this endpoint can give. The compiler checks
    // we only return those, and it documents the endpoint for OpenAPI later.
    private static async Task<Results<Accepted<ScanResponse>, ValidationProblem, ProblemHttpResult>> CreateScanAsync(
        CreateScanRequest request,
        IScanStore store,
        IScanQueue queue,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!HttpUrl.TryParse(request.Url, out var url))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = ["Enter an absolute http or https URL, for example https://example.com."],
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
