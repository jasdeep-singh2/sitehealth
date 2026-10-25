using System.Text.Json;

namespace SiteHealth.Cli;

/// <summary>
/// Turns Claude's JSON reply into <see cref="LinkAdvice"/> records, and checks it before we trust it.
/// Structured outputs guarantee the JSON *shape*, but not that the content makes sense,
/// for example that every URL is one we actually asked about.
/// </summary>
public static class LinkAdviceParser
{
    private static readonly string[] AllowedSeverities = ["high", "medium", "low"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // STEP 4b: you write this.
    //
    // Goal: read Claude's JSON and keep only advice we can trust.
    // Tests: tests/SiteHealth.Cli.Tests/LinkAdviceParserTests.cs
    public static IReadOnlyList<LinkAdvice> Parse(string json, IReadOnlyCollection<Uri> askedAbout)
    {
        var reply = JsonSerializer.Deserialize<AdviceReply>(json, JsonOptions);
        var advice = new List<LinkAdvice>();

        foreach (var item in reply?.Advice ?? [])
        {
            // Rule 1: skip URLs that aren't valid or that we never asked about
            if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var url) || !askedAbout.Contains(url))
                continue;

            // Rule 2: skip unknown severities (only "high", "medium", "low" are allowed)
            if (!AllowedSeverities.Contains(item.Severity))
                continue;

            // Rule 3: skip empty explanations
            if (string.IsNullOrWhiteSpace(item.Cause) || string.IsNullOrWhiteSpace(item.Fix))
                continue;

            // Trim extra spaces before saving
            advice.Add(new LinkAdvice(url, item.Cause.Trim(), item.Fix.Trim(), item.Severity));
        }

        return advice;
    }

    // The raw JSON shape, matching BrokenLinkPrompt.ResponseSchema
    private sealed record AdviceReply(List<AdviceItem>? Advice);

    private sealed record AdviceItem(string Url, string Cause, string Fix, string Severity);
}
