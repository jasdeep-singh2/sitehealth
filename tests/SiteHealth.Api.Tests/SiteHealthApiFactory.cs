using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SiteHealth.Api.Data;
using SiteHealth.Api.Scans;
using SiteHealth.Core;

namespace SiteHealth.Api.Tests;

/// <summary>
/// Starts the real API in memory, with the real queue, worker and EF Core store, but swaps out the
/// things that would reach the outside world: HTTP requests to websites, Claude, and SQL Server.
/// So the tests are fast, free and give the same result every time.
/// </summary>
/// <remarks>
/// The database is SQLite: a real SQL database in a temporary file, deleted afterwards, so every test
/// starts with an empty one. A file rather than ":memory:", because an in-memory SQLite database
/// lives on a single connection, and the API and the worker would use that connection at the same
/// time, which SQLite doesn't allow. With a file, each DbContext gets its own connection, as in production.
/// </remarks>
public sealed class SiteHealthApiFactory : WebApplicationFactory<Program>
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly Dictionary<string, string?> _settings;
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sitehealth-test-{Guid.NewGuid():N}.db");
    private readonly string _connectionString;

    /// <param name="existingScans">Scans to put in the database before the app (and its worker) starts.</param>
    /// <param name="settings">Configuration values to override, e.g. ["RateLimit:ScansPerMinute"] = "2".</param>
    public SiteHealthApiFactory(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        Action<IServiceCollection>? configureServices = null,
        IEnumerable<Scan>? existingScans = null,
        IDictionary<string, string?>? settings = null)
    {
        _respond = respond;
        _configureServices = configureServices;

        // Tests poll GET /scans/{id} every 25 ms, so the real limits would trip them. Rate-limit tests set their own.
        _settings = new Dictionary<string, string?>
        {
            ["RateLimit:ScansPerMinute"] = "1000",
            ["RateLimit:ReadsPerMinute"] = "100000",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            _settings[key] = value;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString();

        // Create the tables now, before the worker starts and asks for unfinished scans.
        // EnsureCreated builds the schema straight from the model; the migrations are written for SQL Server.
        using var db = new SiteHealthDbContext(new DbContextOptionsBuilder<SiteHealthDbContext>().UseSqlite(_connectionString).Options);
        db.Database.EnsureCreated();
        db.Scans.AddRange(existingScans ?? []);
        db.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development", so appsettings.Development.json (which runs SQL Server migrations) isn't loaded
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(_settings));

        builder.ConfigureTestServices(services =>
        {
            // Point both typed HttpClients at a fake handler instead of the network
            services.AddHttpClient<PageFetcher>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(_respond));
            services.AddHttpClient<LinkChecker>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(_respond));

            // Replace Claude (registered when ANTHROPIC_API_KEY is set on this machine) with a fake
            services.RemoveAll<ILinkAdvisor>();
            services.AddSingleton<ILinkAdvisor, FakeLinkAdvisor>();

            // Replace SQL Server with the in-memory SQLite database. Both registrations must go,
            // otherwise EF Core sees two database providers configured and refuses to start.
            services.RemoveAll<DbContextOptions<SiteHealthDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SiteHealthDbContext>>();
            services.AddDbContextFactory<SiteHealthDbContext>(options => options.UseSqlite(_connectionString));

            _configureServices?.Invoke(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Pooled connections keep the file open; close them so the file can be deleted
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
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
