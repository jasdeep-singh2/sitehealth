namespace SiteHealth.Cli.Tests;

// Step 2: these tests fail until you implement LinkExtractor.ExtractLinks.
public class LinkExtractorTests
{
    private static readonly Uri BaseUri = new("https://example.com/blog/post");
    private readonly LinkExtractor _extractor = new();

    [Fact]
    public void Resolves_relative_links_against_the_page_url()
    {
        const string html = """
            <a href="/about">About</a>
            <a href="contact">Contact</a>
            <a href="https://other.com/page">Other site</a>
            """;

        var links = _extractor.ExtractLinks(html, BaseUri);

        Assert.Equal(
            [
                new Uri("https://example.com/about"),
                new Uri("https://example.com/blog/contact"),
                new Uri("https://other.com/page"),
            ],
            links);
    }

    [Fact]
    public void Ignores_links_that_are_not_http_or_https()
    {
        const string html = """
            <a href="mailto:hi@example.com">Email</a>
            <a href="tel:+441234567890">Call</a>
            <a href="javascript:void(0)">Menu</a>
            <a href="/real">Real link</a>
            """;

        var links = _extractor.ExtractLinks(html, BaseUri);

        Assert.Equal([new Uri("https://example.com/real")], links);
    }

    [Fact]
    public void Ignores_fragment_only_links_and_anchors_without_href()
    {
        const string html = """
            <a href="#top">Back to top</a>
            <a name="section">Named anchor</a>
            <a href="/real">Real link</a>
            """;

        var links = _extractor.ExtractLinks(html, BaseUri);

        Assert.Equal([new Uri("https://example.com/real")], links);
    }

    [Fact]
    public void Removes_duplicates_and_keeps_first_seen_order()
    {
        const string html = """
            <a href="/b">B</a>
            <a href="/a">A</a>
            <a href="/b">B again</a>
            """;

        var links = _extractor.ExtractLinks(html, BaseUri);

        Assert.Equal([new Uri("https://example.com/b"), new Uri("https://example.com/a")], links);
    }
}
