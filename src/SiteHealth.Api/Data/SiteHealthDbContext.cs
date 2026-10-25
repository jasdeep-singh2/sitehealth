using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteHealth.Api.Scans;
using SiteHealth.Core;

namespace SiteHealth.Api.Data;

/// <summary>
/// The EF Core view of the database: one table, Scans. EF Core turns LINQ into SQL and records
/// into rows, so we never write SQL by hand.
/// </summary>
public sealed class SiteHealthDbContext(DbContextOptions<SiteHealthDbContext> options) : DbContext(options)
{
    public DbSet<Scan> Scans => Set<Scan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var scan = modelBuilder.Entity<Scan>();

        scan.HasKey(s => s.Id);

        // Uri is stored as text. 2048 is the longest URL browsers reliably support.
        scan.Property(s => s.Url).HasMaxLength(2048);

        // Store "Completed" rather than 2: readable in the database, and safe if the enum is reordered
        scan.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);

        // The worker looks up unfinished scans when it starts, so index the column it filters on
        scan.HasIndex(s => s.Status);

        // The whole report is stored as one JSON text column. We always load it together with its scan
        // and never search inside it yet, so separate tables for groups and links would add work for
        // no benefit. When the dashboard needs to query across scans, we can split it into tables.
        scan.Property(s => s.Report).HasConversion(
            report => report == null ? null : JsonSerializer.Serialize(report, JsonSerializerOptions.Default),
            json => json == null ? null : JsonSerializer.Deserialize<ScanReport>(json, JsonSerializerOptions.Default));
    }
}
