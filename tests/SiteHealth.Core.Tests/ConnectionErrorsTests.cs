using System.Net;
using System.Security.Authentication;

namespace SiteHealth.Core.Tests;

public class ConnectionErrorsTests
{
    [Fact]
    public void Explains_https_failures_in_plain_words()
    {
        var exception = new HttpRequestException(
            HttpRequestError.SecureConnectionError,
            "The SSL connection could not be established, see inner exception.",
            new AuthenticationException("The remote certificate is invalid."));

        var message = ConnectionErrors.Describe(exception);

        Assert.StartsWith("Couldn't make a secure (HTTPS) connection", message);
        Assert.DoesNotContain("inner exception", message);
    }

    [Fact]
    public void Uses_the_inner_message_when_told_to_see_the_inner_exception()
    {
        var exception = new HttpRequestException(
            "An error occurred, see inner exception.",
            new IOException("The connection was reset by the server."));

        Assert.Equal("The connection was reset by the server.", ConnectionErrors.Describe(exception));
    }

    [Fact]
    public void Keeps_messages_that_already_make_sense()
    {
        var exception = new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known. (example.invalid:443)");

        Assert.Equal("No such host is known. (example.invalid:443)", ConnectionErrors.Describe(exception));
    }

    [Fact]
    public async Task Page_fetcher_reports_the_friendly_message()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException(
            HttpRequestError.SecureConnectionError, "The SSL connection could not be established, see inner exception."));
        var fetcher = new PageFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.StartsWith("Couldn't make a secure (HTTPS) connection", result.Error);
    }
}
