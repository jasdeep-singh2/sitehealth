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
        throw new NotImplementedException("Step 2: extract links from the HTML.");
    }
}
