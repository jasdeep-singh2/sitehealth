namespace SiteHealth.Cli.Tests;

// Step 4b: these tests fail until you fill in the blanks in LinkAdviceParser.Parse.
public class LinkAdviceParserTests
{
    private static readonly Uri OldPage = new("https://example.com/old-page");
    private static readonly Uri DownSite = new("https://down.example.com/");
    private static readonly Uri[] AskedAbout = [OldPage, DownSite];

    [Fact]
    public void Turns_valid_json_into_advice()
    {
        const string json = """
            {"advice": [
              {"url": "https://example.com/old-page", "cause": "Page was moved.", "fix": "Add a redirect.", "severity": "high"}
            ]}
            """;

        var advice = LinkAdviceParser.Parse(json, AskedAbout);

        Assert.Equal(new LinkAdvice(OldPage, "Page was moved.", "Add a redirect.", "high"), Assert.Single(advice));
    }

    [Fact]
    public void Skips_urls_we_did_not_ask_about()
    {
        const string json = """
            {"advice": [
              {"url": "https://made-up.example.com/", "cause": "x", "fix": "y", "severity": "low"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Skips_unknown_severity()
    {
        const string json = """
            {"advice": [
              {"url": "https://example.com/old-page", "cause": "x", "fix": "y", "severity": "catastrophic"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Skips_empty_explanations()
    {
        const string json = """
            {"advice": [
              {"url": "https://example.com/old-page", "cause": "   ", "fix": "Add a redirect.", "severity": "high"},
              {"url": "https://down.example.com/", "cause": "Site is down.", "fix": "", "severity": "medium"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Trims_extra_spaces()
    {
        const string json = """
            {"advice": [
              {"url": "https://down.example.com/", "cause": "  Site is down.  ", "fix": " Remove the link. ", "severity": "medium"}
            ]}
            """;

        var advice = Assert.Single(LinkAdviceParser.Parse(json, AskedAbout));

        Assert.Equal("Site is down.", advice.Cause);
        Assert.Equal("Remove the link.", advice.Fix);
    }
}
