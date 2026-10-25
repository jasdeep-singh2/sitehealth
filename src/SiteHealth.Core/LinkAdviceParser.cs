using System.Text.Json;

namespace SiteHealth.Core;

/// <summary>
/// Turns Claude's JSON reply into <see cref="LinkAdvice"/> records, and checks it before we trust it.
/// Structured outputs guarantee the JSON *shape*, but not that the content makes sense,
/// for example that every group number is one we actually asked about.
/// </summary>
public static class LinkAdviceParser
{
    private static readonly string[] AllowedSeverities = ["high", "medium", "low"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // STEP 4b: you write this.
    //
    // Goal: read Claude's JSON and keep only advice we can trust.
    // Tests: tests/SiteHealth.Core.Tests/LinkAdviceParserTests.cs
    public static IReadOnlyList<LinkAdvice> Parse(string json, IReadOnlyCollection<int> askedAbout)
    {
        var reply = JsonSerializer.Deserialize<AdviceReply>(json, JsonOptions);
        var advice = new List<LinkAdvice>();
        var seen = new HashSet<int>();

        foreach (var item in reply?.Advice ?? [])
        {
            // Rule 1: skip group numbers we never asked about, and keep only the first answer per group
            if (!askedAbout.Contains(item.Group) || !seen.Add(item.Group))
                continue;

            // Rule 2: skip unknown severities (only "high", "medium", "low" are allowed)
            if (!AllowedSeverities.Contains(item.Severity))
                continue;

            // Rule 3: skip empty explanations
            if (string.IsNullOrWhiteSpace(item.Cause) || string.IsNullOrWhiteSpace(item.Fix))
                continue;

            // Trim extra spaces before saving
            advice.Add(new LinkAdvice(item.Group, item.Cause.Trim(), item.Fix.Trim(), item.Severity));
        }

        return advice;
    }

    // The raw JSON shape, matching BrokenLinkPrompt.ResponseSchema
    private sealed record AdviceReply(List<AdviceItem>? Advice);

    private sealed record AdviceItem(int Group, string Cause, string Fix, string Severity);
}
