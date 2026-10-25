using Anthropic;
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

// AI advice is optional: it's only switched on when an API key is available.
// AnthropicClient reads ANTHROPIC_API_KEY itself, so the key never appears in our code or config.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
var aiEnabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
if (aiEnabled)
{
    builder.Services.AddSingleton(_ => new AnthropicClient());
    builder.Services.AddSingleton<ILinkAdvisor, ClaudeLinkAdvisor>();
}

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
var page = await fetcher.FetchAsync(startUrl, cts.Token);
if (!page.IsSuccess)
{
    // Exit code 3 means "couldn't check the site", which is different from 2, "found broken links".
    Console.WriteLine($"Couldn't fetch {startUrl}: {page.Error}");
    return 3;
}

var links = extractor.ExtractLinks(page.Html, startUrl);
Console.WriteLine($"Found {links.Count} links. Checking them...");

var results = await checker.CheckAsync(links, cts.Token);
var broken = results.Where(r => r.IsBroken).ToList();
var groups = LinkGrouper.Group(startUrl, broken);

var adviceByGroup = await GetAdviceAsync(host.Services, startUrl, groups, cts.Token);

Console.WriteLine();
Console.WriteLine($"{broken.Count} broken out of {results.Count} links, in {groups.Count} groups.");
foreach (var group in groups)
{
    var site = group.IsSameSite ? "same site" : "other site";
    var count = group.Links.Count == 1 ? "1 link" : $"{group.Links.Count} links";
    Console.WriteLine();
    Console.WriteLine($"  {group.Problem} · {site} · {count}");

    if (adviceByGroup.TryGetValue(group.Id, out var advice))
    {
        Console.WriteLine($"        Severity: {advice.Severity.ToUpperInvariant()}");
        Console.WriteLine($"        Cause:    {advice.Cause}");
        Console.WriteLine($"        Fix:      {advice.Fix}");
    }

    foreach (var link in group.Links)
        Console.WriteLine($"        - {link.Url}{(link.Error is null ? "" : $"  ({link.Error})")}");
}

return broken.Count == 0 ? 0 : 2;

// Asks Claude about the groups of broken links. AI advice is a bonus: if it isn't configured or fails,
// we say so and still show the link results.
static async Task<Dictionary<int, LinkAdvice>> GetAdviceAsync(
    IServiceProvider services, Uri pageUrl, IReadOnlyList<LinkGroup> groups, CancellationToken cancellationToken)
{
    if (groups.Count == 0)
        return [];

    var advisor = services.GetService<ILinkAdvisor>();
    if (advisor is null)
    {
        Console.WriteLine("Tip: set the ANTHROPIC_API_KEY environment variable to get AI explanations and fixes.");
        return [];
    }

    Console.WriteLine($"Asking Claude about {groups.Count} groups of broken links...");
    try
    {
        var advice = await advisor.AdviseAsync(pageUrl, groups, cancellationToken);
        return advice.ToDictionary(a => a.GroupId);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"AI advice unavailable: {ex.Message}");
        return [];
    }
}

static void ConfigureCrawlerClient(IServiceProvider services, HttpClient client)
{
    var options = services.GetRequiredService<IOptions<CrawlerOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
}
