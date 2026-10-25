namespace SiteHealth.Cli;

/// <summary>
/// Explains groups of broken links and suggests fixes.
/// It's an interface so the app uses Claude, while tests can use a fake that costs nothing.
/// </summary>
public interface ILinkAdvisor
{
    Task<IReadOnlyList<LinkAdvice>> AdviseAsync(
        Uri pageUrl,
        IReadOnlyList<LinkGroup> groups,
        CancellationToken cancellationToken);
}
