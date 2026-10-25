using SiteHealth.Cli.Prompts;

namespace SiteHealth.Cli.Tests;

// Step 4a: these tests fail until you fill in the blanks in BrokenLinkPrompt.BuildUserMessage.
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
    public void Describes_a_link_with_a_status_code()
    {
        LinkCheckResult[] broken = [new(new Uri("https://example.com/old-page"), 404, null)];

        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, broken);

        Assert.Contains("- https://example.com/old-page | status 404", message);
    }

    [Fact]
    public void Describes_a_link_that_never_responded()
    {
        LinkCheckResult[] broken = [new(new Uri("https://down.example.com/"), null, "No such host is known.")];

        var message = BrokenLinkPrompt.BuildUserMessage(PageUrl, broken);

        Assert.Contains("- https://down.example.com/ | no response (No such host is known.)", message);
    }
}
