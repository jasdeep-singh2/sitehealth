namespace SiteHealth.Cli;

/// <summary>
/// Settings for the AI advice step. Bound from the "Ai" section of appsettings.json.
/// The API key is NOT here: it comes from the ANTHROPIC_API_KEY environment variable.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Which Claude model explains the broken links.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>
    /// How much effort Claude puts in: "low", "medium" or "high".
    /// Explaining a 404 is simple, so "low" keeps it fast and cheap.
    /// </summary>
    public string Effort { get; set; } = "low";

    /// <summary>Broken links sent per request. More links per request means fewer API calls.</summary>
    public int MaxLinksPerRequest { get; set; } = 50;

    /// <summary>Upper limit on the length of Claude's reply, in tokens.</summary>
    public int MaxTokens { get; set; } = 16000;
}
