using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;
using SiteHealth.Cli.Prompts;

namespace SiteHealth.Cli;

/// <summary>
/// Asks Claude to explain broken links and suggest fixes.
/// Uses the official Anthropic SDK. The <see cref="AnthropicClient"/> manages its own connections,
/// retries and errors, so it's registered once (singleton) instead of going through IHttpClientFactory.
/// </summary>
public sealed class ClaudeLinkAdvisor(AnthropicClient client, IOptions<AiOptions> options) : ILinkAdvisor
{
    private static readonly Dictionary<string, JsonElement> Schema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(BrokenLinkPrompt.ResponseSchema)!;

    private readonly AiOptions _options = options.Value;

    public async Task<IReadOnlyList<LinkAdvice>> AdviseAsync(
        Uri pageUrl,
        IReadOnlyList<LinkCheckResult> brokenLinks,
        CancellationToken cancellationToken)
    {
        var advice = new List<LinkAdvice>();

        // Send the links in batches, so a page with hundreds of broken links doesn't make one giant request
        foreach (var batch in brokenLinks.Chunk(_options.MaxLinksPerRequest))
        {
            var request = new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = BrokenLinkPrompt.System,
                Messages = [new BetaUserMessageParam { Content = BrokenLinkPrompt.BuildUserMessage(pageUrl, batch) }],
                OutputConfig = new BetaOutputConfig
                {
                    Effort = _options.Effort,
                    Format = new BetaJsonOutputFormat { Schema = Schema },
                },
                // If a safety check declines the request, Anthropic re-runs it on a suitable model automatically
                Fallbacks = new Default(),
                Betas = ["server-side-fallback-2026-07-01"],
            };

            var response = await client.Beta.Messages.Create(request, cancellationToken);

            if (response.StopReason == "refusal")
                throw new InvalidOperationException("Claude declined to answer this request.");

            var json = string.Concat(response.Content
                .Select(block => block.TryPickText(out var text) ? text.Text : ""));

            advice.AddRange(LinkAdviceParser.Parse(json, batch.Select(link => link.Url).ToList()));
        }

        return advice;
    }
}
