namespace SiteHealth.Api.Scans;

/// <summary>Settings for the scan queue. Bound from the "ScanQueue" section of appsettings.json.</summary>
public sealed class ScanQueueOptions
{
    public const string SectionName = "ScanQueue";

    /// <summary>How many scans can wait at once. When it's full, POST /scans answers 503.</summary>
    public int Capacity { get; set; } = 100;
}
