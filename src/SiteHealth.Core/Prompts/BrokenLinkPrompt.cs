using System.Text;

namespace SiteHealth.Core.Prompts;

/// <summary>
/// Everything we tell Claude about broken links, kept in one place (see CLAUDE.md: "keep prompts in their own files").
/// </summary>
public static class BrokenLinkPrompt
{
    /// <summary>
    /// How many example URLs we show per group. A few examples give Claude enough context;
    /// sending all 38 identical 404s would only cost tokens.
    /// </summary>
    public const int MaxExamplesPerGroup = 3;

    /// <summary>
    /// The system prompt: who Claude is in this conversation and what a good answer looks like.
    /// </summary>
    public const string System = """
        You are a website maintenance assistant. You help web developers fix broken links.

        The broken links are grouped: every link in a group has the same problem and is either
        on the same site as the checked page or on another site. For each group, explain:
        - cause: the most likely reason these links are broken, in one or two plain sentences.
          Base it on the status code or error. Don't claim to know things you can't see.
        - fix: one practical action the developer can take, such as adding redirects,
          updating or removing the links, or checking whether the other site is down.
        - severity: "high" for links on the same site (users hit a dead end and it hurts SEO),
          "medium" for links to other sites, "low" for minor cases such as a temporary timeout.

        Return exactly one entry per group, using the group number exactly as given.
        """;

    /// <summary>
    /// The user message: the facts about this page and its groups of broken links.
    /// One line per group, for example:
    ///   - group 1 | same site | status 404 | 38 links | e.g. https://example.com/a, https://example.com/b, https://example.com/c
    ///   - group 2 | other site | no response (No such host is known.) | 1 link | e.g. https://down.example.org/
    /// </summary>
    public static string BuildUserMessage(Uri pageUrl, IEnumerable<LinkGroup> groups)
    {
        var message = new StringBuilder();
        message.AppendLine($"Page checked: {pageUrl}");
        message.AppendLine();
        message.AppendLine("Groups of broken links found on this page:");

        foreach (var group in groups)
        {
            var site = group.IsSameSite ? "same site" : "other site";

            // For links that never answered, the first error message tells Claude more than "no response" alone
            var problem = group.Problem == "no response" && group.Links[0].Error is { } error
                ? $"no response ({error})"
                : group.Problem;

            var count = group.Links.Count == 1 ? "1 link" : $"{group.Links.Count} links";
            var examples = string.Join(", ", group.Links.Take(MaxExamplesPerGroup).Select(link => link.Url));

            message.AppendLine($"- group {group.Id} | {site} | {problem} | {count} | e.g. {examples}");
        }

        return message.ToString();
    }

    /// <summary>
    /// The JSON shape Claude must reply with. Structured outputs guarantee the reply matches it.
    /// </summary>
    public const string ResponseSchema = """
        {
          "type": "object",
          "properties": {
            "advice": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "group": { "type": "integer" },
                  "cause": { "type": "string" },
                  "fix": { "type": "string" },
                  "severity": { "type": "string", "enum": ["high", "medium", "low"] }
                },
                "required": ["group", "cause", "fix", "severity"],
                "additionalProperties": false
              }
            }
          },
          "required": ["advice"],
          "additionalProperties": false
        }
        """;
}
