using System.Text;

namespace SiteHealth.Cli.Prompts;

/// <summary>
/// Everything we tell Claude about broken links, kept in one place (see CLAUDE.md: "keep prompts in their own files").
/// </summary>
public static class BrokenLinkPrompt
{
    /// <summary>
    /// The system prompt: who Claude is in this conversation and what a good answer looks like.
    /// </summary>
    public const string System = """
        You are a website maintenance assistant. You help web developers fix broken links.

        For each broken link you are given, explain:
        - cause: the most likely reason it is broken, in one or two plain sentences.
          Base it on the status code or error. Don't claim to know things you can't see.
        - fix: one practical action the developer can take, such as adding a redirect,
          updating or removing the link, or checking whether the other site is down.
        - severity: "high" for links to pages on the same site (users hit a dead end and it hurts SEO),
          "medium" for links to other sites, "low" for minor cases such as a temporary timeout.

        Return exactly one entry per broken link, using the link's URL exactly as given.
        """;

    /// <summary>
    /// The user message: the facts about this page and its broken links.
    /// One line per link, for example:
    ///   - https://example.com/old-page | status 404
    ///   - https://down.example.com/ | no response (No such host is known.)
    /// </summary>
    // STEP 4a: you write this.
    //
    // Goal: build the text we send to Claude. Tests: tests/SiteHealth.Cli.Tests/BrokenLinkPromptTests.cs
    public static string BuildUserMessage(Uri pageUrl, IEnumerable<LinkCheckResult> brokenLinks)
    {
        var message = new StringBuilder();
        message.AppendLine($"Page checked: {pageUrl}");
        message.AppendLine();
        message.AppendLine("Broken links found on this page:");

        foreach (var link in brokenLinks)
        {
            // If there's a status code, say "status 404". If not, say "no response (the error)".
            var problem = link.StatusCode is int status
                ? $"status {status}"                 // Blank 1
                : $"no response ({link.Error})";         // Blank 2

            message.AppendLine($"- {link.Url} | {problem}");   // Blank 3
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
                  "url": { "type": "string" },
                  "cause": { "type": "string" },
                  "fix": { "type": "string" },
                  "severity": { "type": "string", "enum": ["high", "medium", "low"] }
                },
                "required": ["url", "cause", "fix", "severity"],
                "additionalProperties": false
              }
            }
          },
          "required": ["advice"],
          "additionalProperties": false
        }
        """;
}
