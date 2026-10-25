namespace SiteHealth.Cli.Tests;

// These tests fail until you implement LinkGrouper.Group.
public class LinkGrouperTests
{
    private static readonly Uri PageUrl = new("https://example.com/blog");

    [Fact]
    public void Returns_no_groups_when_nothing_is_broken()
    {
        Assert.Empty(LinkGrouper.Group(PageUrl, []));
    }

    [Fact]
    public void Puts_links_with_the_same_problem_in_one_group()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://example.com/b"), 404, null),
            new(new Uri("https://example.com/a"), 404, null),
        ];

        var group = Assert.Single(LinkGrouper.Group(PageUrl, broken));

        Assert.Equal(1, group.Id);
        Assert.True(group.IsSameSite);
        Assert.Equal("status 404", group.Problem);
        Assert.Equal(2, group.Links.Count);
    }

    [Fact]
    public void Keeps_same_site_and_other_site_links_apart()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://example.com/old"), 404, null),
            new(new Uri("https://other.example.org/old"), 404, null),
        ];

        var groups = LinkGrouper.Group(PageUrl, broken);

        Assert.Equal(2, groups.Count);
        Assert.Single(groups, g => g.IsSameSite);
        Assert.Single(groups, g => !g.IsSameSite);
    }

    [Fact]
    public void Treats_the_host_case_insensitively()
    {
        LinkCheckResult[] broken = [new(new Uri("https://EXAMPLE.com/old"), 404, null)];

        Assert.True(Assert.Single(LinkGrouper.Group(PageUrl, broken)).IsSameSite);
    }

    [Fact]
    public void Describes_links_that_never_responded()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://slow.example.org/"), null, "Timed out"),
            new(new Uri("https://down.example.org/"), null, "No such host is known."),
        ];

        var problems = LinkGrouper.Group(PageUrl, broken).Select(g => g.Problem).ToList();

        Assert.Contains("timed out", problems);
        Assert.Contains("no response", problems);
    }

    [Fact]
    public void Orders_same_site_groups_first_then_biggest_first_and_numbers_them_in_that_order()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://other.example.org/1"), 404, null),
            new(new Uri("https://other.example.org/2"), 404, null),
            new(new Uri("https://other.example.org/3"), 404, null),
            new(new Uri("https://example.com/private"), 403, null),
            new(new Uri("https://example.com/old-1"), 404, null),
            new(new Uri("https://example.com/old-2"), 404, null),
        ];

        var groups = LinkGrouper.Group(PageUrl, broken);

        Assert.Equal(["status 404", "status 403", "status 404"], groups.Select(g => g.Problem));
        Assert.Equal([true, true, false], groups.Select(g => g.IsSameSite));
        Assert.Equal([1, 2, 3], groups.Select(g => g.Id));
    }

    [Fact]
    public void Orders_equal_sized_groups_by_problem()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://example.com/b"), 500, null),
            new(new Uri("https://example.com/a"), 404, null),
            new(new Uri("https://example.com/c"), 403, null),
        ];

        var groups = LinkGrouper.Group(PageUrl, broken);

        Assert.Equal(["status 403", "status 404", "status 500"], groups.Select(g => g.Problem));
    }

    [Fact]
    public void Sorts_the_links_inside_a_group_by_url()
    {
        LinkCheckResult[] broken =
        [
            new(new Uri("https://example.com/c"), 404, null),
            new(new Uri("https://example.com/a"), 404, null),
            new(new Uri("https://example.com/b"), 404, null),
        ];

        var group = Assert.Single(LinkGrouper.Group(PageUrl, broken));

        Assert.Equal(
            ["https://example.com/a", "https://example.com/b", "https://example.com/c"],
            group.Links.Select(l => l.Url.ToString()));
    }
}
