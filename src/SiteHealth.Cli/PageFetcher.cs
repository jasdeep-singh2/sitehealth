namespace SiteHealth.Cli;

/// <summary>
/// Downloads the HTML of a single page.
/// The <see cref="HttpClient"/> is injected by IHttpClientFactory (a "typed client"), which
/// reuses connections and avoids socket exhaustion from creating a new HttpClient per request.
/// </summary>
public sealed class PageFetcher(HttpClient httpClient)
{
    // STEP 1: you write this.
    //
    // Goal: return the page's HTML as a string.
    //  - Use httpClient.GetAsync(url, cancellationToken).
    //  - If the response isn't a success status, throw (hint: EnsureSuccessStatusCode).
    //  - Read the body with response.Content.ReadAsStringAsync(cancellationToken).
    //  - Dispose the response (a `using var response = ...` does it).
    //
    // Tests that define "done": tests/SiteHealth.Cli.Tests/PageFetcherTests.cs
    public async Task<string> FetchHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
