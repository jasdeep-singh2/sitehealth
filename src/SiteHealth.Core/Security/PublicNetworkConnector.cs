using System.Net;
using System.Net.Sockets;

namespace SiteHealth.Core.Security;

/// <summary>
/// Opens network connections for HttpClient, but only to public IP addresses.
/// </summary>
/// <remarks>
/// It plugs into <see cref="SocketsHttpHandler.ConnectCallback"/>, which HttpClient calls every time it
/// opens a connection: for the page, for every link, and for every redirect it follows. We look up the
/// host's IP addresses ourselves, drop the private ones, and connect to an address we checked. Because the
/// check and the connection use the same lookup, a DNS server can't answer "public" for the check and
/// "private" for the connection (DNS rebinding).
/// </remarks>
public sealed class PublicNetworkConnector(
    Func<string, CancellationToken, Task<IPAddress[]>> resolve,
    Func<IPAddress, bool> isAllowed)
{
    /// <summary>Real DNS and the real <see cref="PublicAddressPolicy"/>. Tests swap these to simulate attacks.</summary>
    public static PublicNetworkConnector Default { get; } =
        new((host, cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken), PublicAddressPolicy.IsPublic);

    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        // An IP address typed into the URL needs no lookup
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolve(host, cancellationToken);

        var allowed = addresses.Where(isAllowed).ToArray();
        if (allowed.Length == 0)
        {
            // HttpClient wraps this in an HttpRequestException and adds "(host:port)", so it reaches
            // LinkChecker and PageFetcher like any other connection failure
            throw new HttpRequestException($"Blocked: {host} points to a private or internal address");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
