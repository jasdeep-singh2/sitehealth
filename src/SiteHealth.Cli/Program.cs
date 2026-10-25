using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteHealth.Cli;
using SiteHealth.Core;

if (args.Length != 1 || !HttpUrl.TryParse(args[0], out var startUrl))
{
    Console.WriteLine("Usage: dotnet run --project src/SiteHealth.Cli -- https://your-site.com");
    return 1;
}

// The generic host gives us dependency injection, configuration (appsettings.json, environment
// variables) and logging: the same building blocks the API and workers use.
// ContentRootPath points at the build output so appsettings.json is found from any folder.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddSiteHealthCore(builder.Configuration);

using var host = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = host.Services.GetRequiredService<ScanRunner>();
var report = await runner.RunAsync(startUrl, new ConsoleProgress(), cts.Token);

if (report.FetchError is not null)
{
    // Exit code 3 means "couldn't check the site", which is different from 2, "found broken links".
    Console.WriteLine($"Couldn't fetch {startUrl}: {report.FetchError}");
    return 3;
}

Console.WriteLine();
Console.WriteLine($"{report.BrokenCount} broken out of {report.LinksChecked} links, in {report.Groups.Count} groups.");
foreach (var group in report.Groups)
{
    var site = group.IsSameSite ? "same site" : "other site";
    var count = group.Links.Count == 1 ? "1 link" : $"{group.Links.Count} links";
    Console.WriteLine();
    Console.WriteLine($"  {group.Problem} · {site} · {count}");

    if (report.AdviceByGroup.TryGetValue(group.Id, out var advice))
    {
        Console.WriteLine($"        Severity: {advice.Severity.ToUpperInvariant()}");
        Console.WriteLine($"        Cause:    {advice.Cause}");
        Console.WriteLine($"        Fix:      {advice.Fix}");
    }

    foreach (var link in group.Links)
        Console.WriteLine($"        - {link.Url}{(link.Error is null ? "" : $"  ({link.Error})")}");
}

return report.BrokenCount == 0 ? 0 : 2;
