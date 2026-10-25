using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SiteHealth.Core;

namespace SiteHealth.Api.Tests;

/// <summary>
/// Starts the real API in memory, with the real queue, store and worker, but swaps out the two
/// things that would reach the outside world: HTTP requests to websites, and Claude.
/// So the tests are fast, free and give the same result every time.
/// </summary>
public sealed class SiteHealthApiFactory(
    Func<HttpRequestMessage, HttpResponseMessage> respond,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Point both typed HttpClients at a fake handler instead of the network
            services.AddHttpClient<PageFetcher>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(respond));
            services.AddHttpClient<LinkChecker>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(respond));

            // Replace Claude (registered when ANTHROPIC_API_KEY is set on this machine) with a fake
            services.RemoveAll<ILinkAdvisor>();
            services.AddSingleton<ILinkAdvisor, FakeLinkAdvisor>();

            configureServices?.Invoke(services);
        });
    }

    private sealed class FakeLinkAdvisor : ILinkAdvisor
    {
        public Task<IReadOnlyList<LinkAdvice>> AdviseAsync(
            Uri pageUrl, IReadOnlyList<LinkGroup> groups, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LinkAdvice>>(
                groups.Select(g => new LinkAdvice(g.Id, "The page was removed.", "Add a redirect.", "high")).ToList());
    }
}

/// <summary>A fake HTTP handler: the test decides the response to every request.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
