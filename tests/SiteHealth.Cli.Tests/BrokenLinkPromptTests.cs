using SiteHealth.Cli.Prompts;

namespace SiteHealth.Cli.Tests;

public class BrokenLinkPromptTests
{
    private static readonly Uri PageUrl = new("https://example.com/blog");

    [Fact]
    public void Mentions_the_page_that_was_checked()
    {
        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, []);

        Assert.Contains("Page checked: https://example.com/blog", message);
    }

    [Fact]
    public void Describes_a_group_with_a_status_code()
    {
        LinkGroup[] groups =
        [
            new(1, true, "status 404",
            [
                new(new Uri("https://example.com/a"), 404, null),
                new(new Uri("https://example.com/b"), 404, null),
            ]),
        ];

        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, groups);

        Assert.Contains("- group 1 | same site | status 404 | 2 links | e.g. https://example.com/a, https://example.com/b", message);
    }

    [Fact]
    public void Includes_the_error_for_links_that_never_responded()
    {
        LinkGroup[] groups =
        [
            new(2, false, "no response", [new(new Uri("https://down.example.org/"), null, "No such host is known.")]),
        ];

        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, groups);

        Assert.Contains("- group 2 | other site | no response (No such host is known.) | 1 link | e.g. https://down.example.org/", message);
    }

    [Fact]
    public void Sends_only_a_few_example_urls_per_group()
    {
        var links = Enumerable.Range(1, 10)
            .Select(i => new LinkCheckResult(new Uri($"https://example.com/old-{i}"), 404, null))
            .ToList();
        LinkGroup[] groups = [new(1, true, "status 404", links)];

        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, groups);

        Assert.Contains("10 links", message);
        Assert.Contains("https://example.com/old-3", message);
        Assert.DoesNotContain("https://example.com/old-4", message);
    }
}
