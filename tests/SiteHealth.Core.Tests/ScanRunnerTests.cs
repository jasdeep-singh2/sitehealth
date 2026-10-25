using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SiteHealth.Core.Tests;

public class ScanRunnerTests
{
    private static readonly Uri PageUrl = new("https://example.com/");

    private const string PageHtml = """
        <html><body>
          <a href="/ok">ok</a>
          <a href="/old-1">old 1</a>
          <a href="/old-2">old 2</a>
        </body></html>
        """;

    // Serves the page and answers every link check: /ok is fine, the /old-* pages are 404.
    private static HttpResponseMessage Respond(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PageHtml) },
        "/ok" => new HttpResponseMessage(HttpStatusCode.OK),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    };

    private static ScanRunner CreateRunner(Func<HttpRequestMessage, HttpResponseMessage> respond, ILinkAdvisor? advisor = null)
    {
        var handler = new StubHttpMessageHandler(respond);
        return new ScanRunner(
            new PageFetcher(new HttpClient(handler)),
            new LinkExtractor(),
            new LinkChecker(new HttpClient(handler), Options.Create(new CrawlerOptions())),
            advisor);
    }

    [Fact]
    public async Task Checks_the_links_and_groups_the_broken_ones()
    {
        var report = await CreateRunner(Respond).RunAsync(PageUrl, null, CancellationToken.None);

        Assert.Null(report.FetchError);
        Assert.Equal(3, report.LinksChecked);
        Assert.Equal(2, report.BrokenCount);
        var group = Assert.Single(report.Groups);
        Assert.Equal("status 404", group.Problem);
        Assert.Equal(2, group.Links.Count);
    }

    [Fact]
    public async Task Stops_with_an_error_when_the_page_cannot_be_fetched()
    {
        var runner = CreateRunner(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var report = await runner.RunAsync(PageUrl, null, CancellationToken.None);

        Assert.NotNull(report.FetchError);
        Assert.Equal(0, report.LinksChecked);
        Assert.Empty(report.Groups);
    }

    [Fact]
    public async Task Adds_the_advisors_advice_by_group()
    {
        var advisor = new FakeAdvisor(groups => groups.Select(g => new LinkAdvice(g.Id, "Moved.", "Redirect.", "high")).ToList());

        var report = await CreateRunner(Respond, advisor).RunAsync(PageUrl, null, CancellationToken.None);

        Assert.Equal("Moved.", report.AdviceByGroup[report.Groups[0].Id].Cause);
    }

    [Fact]
    public async Task Still_returns_the_links_when_the_advisor_fails()
    {
        var advisor = new FakeAdvisor(_ => throw new InvalidOperationException("API is down"));
        var progress = new ListProgress();

        var report = await CreateRunner(Respond, advisor).RunAsync(PageUrl, progress, CancellationToken.None);

        Assert.Single(report.Groups);
        Assert.Empty(report.AdviceByGroup);
        Assert.Contains("AI advice unavailable: API is down", progress.Messages);
    }

    [Fact]
    public async Task Reports_progress_in_order()
    {
        var progress = new ListProgress();

        await CreateRunner(Respond).RunAsync(PageUrl, progress, CancellationToken.None);

        Assert.Equal(
            [
                "Fetching https://example.com/ ...",
                "Found 3 links. Checking them...",
                "Tip: set the ANTHROPIC_API_KEY environment variable to get AI explanations and fixes.",
            ],
            progress.Messages);
    }

    [Fact]
    public void Can_be_created_from_dependency_injection()
    {
        // Proves AddSiteHealthCore registers everything ScanRunner needs, with or without an AI advisor.
        var services = new ServiceCollection()
            .AddSiteHealthCore(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        Assert.NotNull(services.GetRequiredService<ScanRunner>());
    }

    private sealed class FakeAdvisor(Func<IReadOnlyList<LinkGroup>, IReadOnlyList<LinkAdvice>> advise) : ILinkAdvisor
    {
        public Task<IReadOnlyList<LinkAdvice>> AdviseAsync(
            Uri pageUrl, IReadOnlyList<LinkGroup> groups, CancellationToken cancellationToken) =>
            Task.FromResult(advise(groups));
    }

    private sealed class ListProgress : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value) => Messages.Add(value);
    }
}
