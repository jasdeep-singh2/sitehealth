namespace SiteHealth.Cli.Tests;

public class LinkAdviceParserTests
{
    private static readonly int[] AskedAbout = [1, 2];

    [Fact]
    public void Turns_valid_json_into_advice()
    {
        const string json = """
            {"advice": [
              {"group": 1, "cause": "Pages were moved.", "fix": "Add redirects.", "severity": "high"}
            ]}
            """;

        var advice = LinkAdviceParser.Parse(json, AskedAbout);

        Assert.Equal(new LinkAdvice(1, "Pages were moved.", "Add redirects.", "high"), Assert.Single(advice));
    }

    [Fact]
    public void Skips_groups_we_did_not_ask_about()
    {
        const string json = """
            {"advice": [
              {"group": 99, "cause": "x", "fix": "y", "severity": "low"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Keeps_only_the_first_answer_for_a_group()
    {
        const string json = """
            {"advice": [
              {"group": 1, "cause": "First.", "fix": "Fix one.", "severity": "high"},
              {"group": 1, "cause": "Second.", "fix": "Fix two.", "severity": "low"}
            ]}
            """;

        Assert.Equal("First.", Assert.Single(LinkAdviceParser.Parse(json, AskedAbout)).Cause);
    }

    [Fact]
    public void Skips_unknown_severity()
    {
        const string json = """
            {"advice": [
              {"group": 1, "cause": "x", "fix": "y", "severity": "catastrophic"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Skips_empty_explanations()
    {
        const string json = """
            {"advice": [
              {"group": 1, "cause": "   ", "fix": "Add redirects.", "severity": "high"},
              {"group": 2, "cause": "Site is down.", "fix": "", "severity": "medium"}
            ]}
            """;

        Assert.Empty(LinkAdviceParser.Parse(json, AskedAbout));
    }

    [Fact]
    public void Trims_extra_spaces()
    {
        const string json = """
            {"advice": [
              {"group": 2, "cause": "  Site is down.  ", "fix": " Remove the link. ", "severity": "medium"}
            ]}
            """;

        var advice = Assert.Single(LinkAdviceParser.Parse(json, AskedAbout));

        Assert.Equal("Site is down.", advice.Cause);
        Assert.Equal("Remove the link.", advice.Fix);
    }
}
