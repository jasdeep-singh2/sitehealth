namespace SiteHealth.Core;

/// <summary>
/// Settings for how politely we crawl. Bound from the "Crawler" section of appsettings.json
/// (the Options pattern), so they can change without touching code.
/// </summary>
public sealed class CrawlerOptions
{
    public const string SectionName = "Crawler";

    /// <summary>How many links we check at the same time. Keep it low so we don't hammer the site.</summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>How long to wait for a single request before giving up.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Identifies our crawler to site owners, with a link back to the project.</summary>
    public string UserAgent { get; set; } = "SiteHealthBot/0.1 (+https://github.com/jasdeep-singh2/sitehealth)";

    /// <summary>
    /// Allow scanning localhost, private networks and other internal addresses. Off by default, so a
    /// public server is safe from SSRF without extra setup. The CLI switches it on, because on your own
    /// machine scanning your local dev site (http://localhost:3000) is exactly what you want.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }
}
