using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SiteHealth.Api.Data;
using SiteHealth.Api.Scans;

namespace SiteHealth.Api.Tests;

public class DatabaseSetupTests
{
    [Fact]
    public async Task Retries_temporary_sql_server_errors()
    {
        // The real registration, without the test factory's SQLite swap. Nothing connects to SQL Server here.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SiteHealth"] = "Server=(localdb)\\MSSQLLocalDB;Database=Unused;Trusted_Connection=True",
            })
            .Build();
        await using var services = new ServiceCollection().AddLogging().AddScans(configuration).BuildServiceProvider();

        await using var db = await services.GetRequiredService<IDbContextFactory<SiteHealthDbContext>>().CreateDbContextAsync();

#pragma warning disable EF1001 // SqlServerRetryingExecutionStrategy is in an "Internal" namespace; we only check its type
        Assert.IsType<SqlServerRetryingExecutionStrategy>(db.Database.CreateExecutionStrategy());
#pragma warning restore EF1001
    }
}
