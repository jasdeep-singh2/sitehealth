using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SiteHealth.Api.Data;
using SiteHealth.Api.Scans;
using SiteHealth.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiteHealthCore(builder.Configuration);
builder.Services.AddScans(builder.Configuration);
builder.Services.AddScanRateLimits(builder.Configuration);

// Errors come back as ProblemDetails (RFC 9457): one standard JSON shape for every error
builder.Services.AddProblemDetails();

// Send enums as text ("Completed") instead of numbers (2), so the JSON is readable
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// On your own machine, create or update the database automatically (see DatabaseOptions)
if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
{
    await using var db = await app.Services.GetRequiredService<IDbContextFactory<SiteHealthDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapScanEndpoints();

await app.RunAsync();

// Lets the integration tests start this app in memory with WebApplicationFactory<Program>
public partial class Program;
