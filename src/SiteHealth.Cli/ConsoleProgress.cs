namespace SiteHealth.Cli;

/// <summary>
/// Prints scan status messages straight away, in order.
/// We don't use the built-in <see cref="Progress{T}"/> here: it hands each message to the thread
/// pool, so in a console app messages could print late or out of order.
/// </summary>
internal sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string value) => Console.WriteLine(value);
}
