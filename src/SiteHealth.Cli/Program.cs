using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SiteHealth.Cli;

if (args.Length != 1
    || !Uri.TryCreate(args[0], UriKind.Absolute, out var startUrl)
    || (startUrl.Scheme != Uri.UriSchemeHttp && startUrl.Scheme != Uri.UriSchemeHttps))
{
    Console.WriteLine("Usage: dotnet run --project src/SiteHealth.Cli -- https://your-site.com");
    return 1;
}

// The generic host gives us dependency injection, configuration (appsettings.json, environment
// variables) and logging: the same building blocks the API and workers will use later.
// ContentRootPath points at the build output so appsettings.json is found from any folder.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection(CrawlerOptions.SectionName));
builder.Services.AddSingleton<LinkExtractor>();
builder.Services.AddHttpClient<PageFetcher>(ConfigureCrawlerClient);
builder.Services.AddHttpClient<LinkChecker>(ConfigureCrawlerClient);

using var host = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var fetcher = host.Services.GetRequiredService<PageFetcher>();
var extractor = host.Services.GetRequiredService<LinkExtractor>();
var checker = host.Services.GetRequiredService<LinkChecker>();

Console.WriteLine($"Fetching {startUrl} ...");
var html = await fetcher.FetchHtmlAsync(startUrl, cts.Token);

var links = extractor.ExtractLinks(html, startUrl);
Console.WriteLine($"Found {links.Count} links. Checking them...");

var results = await checker.CheckAsync(links, cts.Token);
var broken = results.Where(r => r.IsBroken).OrderBy(r => r.Url.ToString()).ToList();

Console.WriteLine();
Console.WriteLine($"{broken.Count} broken out of {results.Count} links.");
foreach (var result in broken)
{
    var status = result.StatusCode?.ToString() ?? "ERR";
    Console.WriteLine($"  {status,-5} {result.Url}{(result.Error is null ? "" : $"  ({result.Error})")}");
}

// STEP 4 (next): send `broken` to Claude and print an explanation and suggested fix for each link.

return broken.Count == 0 ? 0 : 2;

static void ConfigureCrawlerClient(IServiceProvider services, HttpClient client)
{
    var options = services.GetRequiredService<IOptions<CrawlerOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
}
