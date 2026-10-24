namespace SiteHealth.Cli;

/// <summary>
/// Finds every link on a page and turns it into an absolute http(s) URL.
/// </summary>
public sealed class LinkExtractor
{
    // STEP 2: you write this.
    //
    // Goal: return the unique, absolute http/https links found in <a href="..."> elements.
    //  - Parse the HTML with AngleSharp: new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html)
    //  - Select anchors with href: document.QuerySelectorAll("a[href]")
    //  - Resolve relative hrefs against baseUri: Uri.TryCreate(baseUri, href, out var absolute)
    //  - Keep only http and https schemes (skip mailto:, tel:, javascript:).
    //  - Skip links that only point to a fragment on the same page (href="#top").
    //  - Remove duplicates, keeping the order they appear in.
    //
    // Tests that define "done": tests/SiteHealth.Cli.Tests/LinkExtractorTests.cs
    public IReadOnlyList<Uri> ExtractLinks(string html, Uri baseUri)
    {
        // 1. Turn the HTML text into a document we can search
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        var links = new List<Uri>();
        var seen = new HashSet<Uri>();   // remembers links we've already added

        // 2. Go through every <a> tag that has an href
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");

            // Rule 3: skip empty links and links that start with "#"
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith("#"))
                continue;

            // Rule 1: combine the page address with the link, e.g. "/about" → full address
            if (!Uri.TryCreate(baseUri, href, out var absolute))
                continue;

            // Rule 2: keep only http and https
            if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                continue;

            // Rule 4: seen.Add returns false if the link is already there
            if (seen.Add(absolute))
                links.Add(absolute);
        }

        return links;
    }
}
