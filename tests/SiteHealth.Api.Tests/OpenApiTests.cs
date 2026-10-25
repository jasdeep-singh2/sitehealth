using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SiteHealth.Api.Tests;

public class OpenApiTests
{
    private static HttpResponseMessage NoWebsite(HttpRequestMessage _) => new(HttpStatusCode.NotFound);

    // Development, but without the SQL Server migrations that appsettings.Development.json switches on
    private static SiteHealthApiFactory DevelopmentApi() => new(
        NoWebsite,
        settings: new Dictionary<string, string?> { ["Database:MigrateOnStartup"] = "false" },
        environment: "Development");

    [Fact]
    public async Task Describes_both_endpoints_with_their_responses()
    {
        await using var factory = DevelopmentApi();
        var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var post = document.GetProperty("paths").GetProperty("/scans").GetProperty("post");
        Assert.Equal("CreateScan", post.GetProperty("operationId").GetString());
        Assert.Equal(["202", "400", "429", "503"], post.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order());

        var get = document.GetProperty("paths").GetProperty("/scans/{id}").GetProperty("get");
        Assert.Equal("GetScan", get.GetProperty("operationId").GetString());
        Assert.Equal(["200", "404", "429"], get.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order());
    }

    [Fact]
    public async Task Describes_status_as_text_and_numbers_as_plain_integers()
    {
        await using var factory = DevelopmentApi();
        var client = factory.CreateClient();

        var schemas = (await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json"))
            .GetProperty("components").GetProperty("schemas");

        Assert.Equal(
            ["Queued", "Running", "Completed", "Failed"],
            schemas.GetProperty("ScanStatus").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("integer", schemas.GetProperty("ScanResult").GetProperty("properties").GetProperty("linksChecked")
            .GetProperty("type").GetString());
    }

    [Fact]
    public async Task Serves_the_scalar_page_in_development()
    {
        await using var factory = DevelopmentApi();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Does_not_serve_the_document_outside_development()
    {
        await using var factory = new SiteHealthApiFactory(NoWebsite);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar")).StatusCode);
    }
}
