namespace SiteHealth.Api.Data;

/// <summary>Database settings. Bound from the "Database" section of appsettings.json.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply EF Core migrations when the app starts. Handy on your own machine (switched on in
    /// appsettings.Development.json); in Azure, migrations will run from the deployment pipeline instead.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
