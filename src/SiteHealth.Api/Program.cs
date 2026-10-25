using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using SiteHealth.Api.Data;
using SiteHealth.Api.Scans;
using SiteHealth.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiteHealthCore(builder.Configuration);
builder.Services.AddScans(builder.Configuration);
builder.Services.AddScanRateLimits(builder.Configuration);

// Errors come back as ProblemDetails (RFC 9457): one standard JSON shape for every error
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Send enums as text ("Completed") instead of numbers (2), so the JSON is readable
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // Numbers are numbers. The web default also accepts "404" as text, which makes OpenAPI describe every
    // number as "integer or string" and the generated TypeScript types as number | string.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// OpenAPI: a machine-readable description of every endpoint, request and response. Built from the
// endpoints themselves, so it can't fall out of date. The web app generates its TypeScript types from it.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "SiteHealth API";
    document.Info.Description = "Start a scan of a web page and check on it. A scan finds broken links, "
        + "groups them by problem and, when AI is switched on, explains each group with a suggested fix.";
    return Task.CompletedTask;
}));

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

// On your own machine: the document at /openapi/v1.json, and Scalar at /scalar, a page for reading the
// API and trying requests in the browser. Not mapped in production yet; the build writes the document
// to a file for anything that needs it.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();

// Lets the integration tests start this app in memory with WebApplicationFactory<Program>
public partial class Program;
