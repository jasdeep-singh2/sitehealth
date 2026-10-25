namespace SiteHealth.Core;

/// <summary>
/// Downloads the HTML of a single page.
/// The <see cref="HttpClient"/> is injected by IHttpClientFactory (a "typed client"), which
/// reuses connections and avoids socket exhaustion from creating a new HttpClient per request.
/// </summary>
public sealed class PageFetcher(HttpClient httpClient)
{
    // Returns a PageFetchResult and never throws because the page couldn't be fetched.
    // This is the Result pattern: an unreachable site is an expected outcome, so it's data
    // the caller can test and report, not an exception that crashes the CLI.
    public async Task<PageFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            var statusCode = (int)response.StatusCode;

            // The server answered, but not with the page: keep the status so the caller can show it.
            if (!response.IsSuccessStatusCode)
                return new PageFetchResult(url, null, statusCode, $"{statusCode} {response.ReasonPhrase}".TrimEnd());

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return new PageFetchResult(url, html, statusCode, null);
        }
        catch (HttpRequestException ex)
        {
            // No response at all (DNS failure, refused connection), so no status code.
            return new PageFetchResult(url, null, null, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout. If our token was cancelled (Ctrl+C), the filter is false
            // and the exception bubbles up, because the caller asked to stop.
            return new PageFetchResult(url, null, null, "Timed out");
        }
    }
}
