using System.Text.Json.Serialization;
using SiteHealth.Api.Scans;
using SiteHealth.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiteHealthCore(builder.Configuration);
builder.Services.AddScans(builder.Configuration);

// Errors come back as ProblemDetails (RFC 9457): one standard JSON shape for every error
builder.Services.AddProblemDetails();

// Send enums as text ("Completed") instead of numbers (2), so the JSON is readable
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapScanEndpoints();

app.Run();

// Lets the integration tests start this app in memory with WebApplicationFactory<Program>
public partial class Program;
