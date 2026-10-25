using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

public class ScanEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string PageHtml = """
        <html><body>
          <a href="/ok">ok</a>
          <a href="/old-1">old 1</a>
          <a href="/old-2">old 2</a>
        </body></html>
        """;

    // A small fake website: the home page links to /ok (fine) and two pages that are gone (404).
    // Any request to broken.example throws, to simulate a bug deep inside a scan.
    private static HttpResponseMessage FakeWebsite(HttpRequestMessage request)
    {
        if (request.RequestUri!.Host == "broken.example")
            throw new InvalidOperationException("Something unexpected went wrong.");

        return request.RequestUri.AbsolutePath switch
        {
            "/" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PageHtml) },
            "/ok" => new HttpResponseMessage(HttpStatusCode.OK),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    [Fact]
    public async Task Post_accepts_the_scan_and_the_worker_completes_it()
    {
        await using var factory = new SiteHealthApiFactory(FakeWebsite);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<ScanResponse>(Json);
        Assert.Equal(ScanStatus.Queued, accepted!.Status);
        Assert.Equal($"/scans/{accepted.Id}", response.Headers.Location!.ToString());

        var scan = await WaitForScanAsync(client, response.Headers.Location);

        Assert.Equal(ScanStatus.Completed, scan.Status);
        Assert.NotNull(scan.CompletedAt);
        Assert.Equal(3, scan.Result!.LinksChecked);
        Assert.Equal(2, scan.Result.BrokenCount);
        var group = Assert.Single(scan.Result.Groups);
        Assert.Equal("status 404", group.Problem);
        Assert.Equal("Add a redirect.", group.Advice!.Fix);
        Assert.Equal(2, group.Links.Count);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/")]
    [InlineData(null)]
    public async Task Post_rejects_an_invalid_url_with_problem_details(string? url)
    {
        await using var factory = new SiteHealthApiFactory(FakeWebsite);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/scans", new CreateScanRequest(url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_scan()
    {
        await using var factory = new SiteHealthApiFactory(FakeWebsite);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/scans/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Scan_fails_with_the_reason_when_the_page_cannot_be_fetched()
    {
        await using var factory = new SiteHealthApiFactory(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));
        var scan = await WaitForScanAsync(client, response.Headers.Location!);

        Assert.Equal(ScanStatus.Failed, scan.Status);
        Assert.Contains("503", scan.Error);
        Assert.Null(scan.Result);
    }

    [Fact]
    public async Task Worker_keeps_going_after_a_scan_fails_unexpectedly()
    {
        await using var factory = new SiteHealthApiFactory(FakeWebsite);
        var client = factory.CreateClient();

        var broken = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://broken.example/"));
        var brokenScan = await WaitForScanAsync(client, broken.Headers.Location!);

        var healthy = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));
        var healthyScan = await WaitForScanAsync(client, healthy.Headers.Location!);

        Assert.Equal(ScanStatus.Failed, brokenScan.Status);
        Assert.Equal("The scan failed unexpectedly.", brokenScan.Error);
        Assert.Equal(ScanStatus.Completed, healthyScan.Status);
    }

    [Fact]
    public async Task Worker_finishes_scans_that_were_interrupted_by_a_restart()
    {
        // As if the app stopped while one scan was waiting and another was halfway through
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        Scan[] interrupted =
        [
            new(Guid.CreateVersion7(), new Uri("https://example.com/"), ScanStatus.Queued, createdAt),
            new(Guid.CreateVersion7(), new Uri("https://example.com/"), ScanStatus.Running, createdAt),
        ];

        await using var factory = new SiteHealthApiFactory(FakeWebsite, existingScans: interrupted);
        var client = factory.CreateClient();

        foreach (var scan in interrupted)
        {
            var finished = await WaitForScanAsync(client, new Uri($"/scans/{scan.Id}", UriKind.Relative));
            Assert.Equal(ScanStatus.Completed, finished.Status);
            Assert.Equal(2, finished.Result!.BrokenCount);
        }
    }

    [Fact]
    public async Task Post_returns_503_when_the_queue_is_full()
    {
        await using var factory = new SiteHealthApiFactory(FakeWebsite, services =>
        {
            services.RemoveAll<IScanQueue>();
            services.AddSingleton<IScanQueue, FullQueue>();
        });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/scans", new CreateScanRequest("https://example.com/"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    // Polls GET /scans/{id} until the worker has finished, like a real client would.
    private static async Task<ScanResponse> WaitForScanAsync(HttpClient client, Uri location)
    {
        var timeout = TimeSpan.FromSeconds(10);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var scan = await client.GetFromJsonAsync<ScanResponse>(location, Json);
            if (scan!.Status is ScanStatus.Completed or ScanStatus.Failed)
                return scan;

            await Task.Delay(25);
        }

        throw new TimeoutException($"Scan at {location} didn't finish within {timeout.TotalSeconds} seconds.");
    }

    // A queue that is always full and never hands anything to the worker
    private sealed class FullQueue : IScanQueue
    {
        public bool TryEnqueue(Guid scanId) => false;

        public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) => AsyncEnumerable.Empty<Guid>();
    }
}
