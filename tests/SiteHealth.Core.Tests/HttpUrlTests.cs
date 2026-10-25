namespace SiteHealth.Core.Tests;

public class HttpUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/blog?page=2")]
    public void Accepts_absolute_http_and_https_urls(string value)
    {
        Assert.True(HttpUrl.TryParse(value, out var url));
        Assert.Equal(new Uri(value), url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/secret.txt")]
    public void Rejects_anything_else(string? value)
    {
        Assert.False(HttpUrl.TryParse(value, out var url));
        Assert.Null(url);
    }
}
