namespace SiteHealth.Cli.Tests;

public class LinkCheckResultTests
{
    private static readonly Uri Url = new("https://example.com/page");

    [Theory]
    [InlineData(200, false)]
    [InlineData(301, false)]
    [InlineData(404, true)]
    [InlineData(500, true)]
    public void IsBroken_depends_on_status_code(int statusCode, bool expected)
    {
        var result = new LinkCheckResult(Url, statusCode, null);

        Assert.Equal(expected, result.IsBroken);
    }

    [Fact]
    public void IsBroken_when_request_failed_without_a_response()
    {
        var result = new LinkCheckResult(Url, null, "Connection refused");

        Assert.True(result.IsBroken);
    }
}
