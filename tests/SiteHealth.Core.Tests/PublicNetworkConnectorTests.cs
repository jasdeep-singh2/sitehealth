using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteHealth.Core.Security;

namespace SiteHealth.Core.Tests;

/// <summary>
/// Tests the SSRF guard against a real web server on this machine, with real sockets, because the
/// guard's job is all about real connections.
/// </summary>
/// <remarks>
/// To simulate attacks we give the connector fake DNS and a test policy:
/// "public.test" resolves to ::1, which the test policy treats as a public address;
/// "internal.test" resolves to 127.0.0.1, which it treats as private.
/// The server listens on both, so the only thing that can stop a request is the guard.
/// </remarks>
public sealed class PublicNetworkConnectorTests : IAsyncLifetime
{
    private WebApplication _server = null!;
    private int _port;

    private static readonly PublicNetworkConnector TestConnector = new(
        (host, _) => host switch
        {
            "public.test" => Task.FromResult(new[] { IPAddress.IPv6Loopback }),
            "internal.test" => Task.FromResult(new[] { IPAddress.Loopback }),
            _ => throw new HttpRequestException($"No such host: {host}"),
        },
        address => address.Equals(IPAddress.IPv6Loopback));

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        // IPv6Any is "dual mode": it accepts both ::1 and 127.0.0.1. Port 0 means "pick a free port".
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.IPv6Any, 0));

        _server = builder.Build();
        _server.MapGet("/", () => "home");
        _server.MapGet("/secret", () => "secret");
        _server.MapGet("/redirect", (string to) => Results.Redirect(to));
        await _server.StartAsync();

        _port = new Uri(_server.Urls.First()).Port;
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    private static HttpClient GuardedClient(PublicNetworkConnector connector) =>
        new(CrawlerHttpHandler.Create(new CrawlerOptions(), connector));

    [Fact]
    public async Task Connects_to_an_allowed_address()
    {
        using var client = GuardedClient(TestConnector);

        Assert.Equal("home", await client.GetStringAsync($"http://public.test:{_port}/"));
    }

    [Fact]
    public async Task Blocks_a_name_that_resolves_to_a_private_address()
    {
        using var client = GuardedClient(TestConnector);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync($"http://internal.test:{_port}/secret"));

        Assert.Contains("Blocked", ex.Message);
    }

    [Fact]
    public async Task Blocks_a_redirect_from_a_public_site_to_a_private_one()
    {
        using var client = GuardedClient(TestConnector);
        var target = Uri.EscapeDataString($"http://internal.test:{_port}/secret");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStringAsync($"http://public.test:{_port}/redirect?to={target}"));

        Assert.Contains("Blocked", ex.Message);
    }

    [Fact]
    public async Task Blocks_a_private_ip_address_in_the_url()
    {
        using var client = GuardedClient(TestConnector);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync($"http://127.0.0.1:{_port}/secret"));

        Assert.Contains("Blocked", ex.Message);
    }

    [Fact]
    public async Task With_real_dns_and_the_real_policy_localhost_is_blocked()
    {
        using var client = new HttpClient(CrawlerHttpHandler.Create(new CrawlerOptions()));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync($"http://localhost:{_port}/secret"));

        Assert.Contains("Blocked", ex.Message);
    }

    [Fact]
    public async Task Allowing_private_networks_reaches_localhost_like_the_cli_needs()
    {
        using var client = new HttpClient(CrawlerHttpHandler.Create(new CrawlerOptions { AllowPrivateNetworks = true }));

        Assert.Equal("home", await client.GetStringAsync($"http://localhost:{_port}/"));
    }

    [Fact]
    public async Task Link_checker_reports_a_blocked_link_as_broken_instead_of_crashing()
    {
        var checker = new LinkChecker(
            new HttpClient(CrawlerHttpHandler.Create(new CrawlerOptions())),
            Options.Create(new CrawlerOptions()));

        var result = Assert.Single(await checker.CheckAsync([new Uri($"http://127.0.0.1:{_port}/secret")], CancellationToken.None));

        Assert.True(result.IsBroken);
        Assert.Null(result.StatusCode);
        Assert.Contains("Blocked", result.Error);
    }
}
