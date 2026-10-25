using System.Reflection;

namespace SiteHealth.Api;

/// <summary>
/// Tells the app when it's only being started by the build to write the OpenAPI document
/// (src/SiteHealth.Api/openapi/openapi.json). The build runs the app inside a tool called
/// GetDocument.Insider, with a dummy web server, and it does start hosted services. Anything that needs
/// real infrastructure, like the database, must be skipped then, or the build fails when the database is down.
/// </summary>
public static class BuildTimeDocument
{
    public static bool IsBeingGenerated { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
