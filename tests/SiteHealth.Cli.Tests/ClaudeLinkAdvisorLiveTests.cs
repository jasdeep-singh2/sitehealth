using Anthropic;
using Microsoft.Extensions.Options;

namespace SiteHealth.Cli.Tests;

// Calls the REAL Claude API, so it costs a few cents and needs ANTHROPIC_API_KEY.
// Skipped unless you opt in by setting SITEHEALTH_LIVE_AI_TESTS=1 in your environment.
public class ClaudeLinkAdvisorLiveTests
{
    [Fact]
    public async Task Claude_returns_valid_advice_for_each_group()
    {
        if (Environment.GetEnvironmentVariable("SITEHEALTH_LIVE_AI_TESTS") != "1")
            return;

        var advisor = new ClaudeLinkAdvisor(new AnthropicClient(), Options.Create(new AiOptions()));
        List<LinkGroup> groups =
        [
            new(1, true, "status 404",
            [
                new(new Uri("https://example.com/old-page"), 404, null),
                new(new Uri("https://example.com/older-page"), 404, null),
            ]),
            new(2, false, "no response", [new(new Uri("https://down.example.invalid/"), null, "No such host is known.")]),
        ];

        var advice = await advisor.AdviseAsync(new Uri("https://example.com/"), groups, CancellationToken.None);

        Assert.Equal(2, advice.Count);
        Assert.All(advice, a => Assert.Contains(a.Severity, new[] { "high", "medium", "low" }));
    }
}
