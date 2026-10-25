using System.Net;
using System.Net.Sockets;

namespace SiteHealth.Core.Security;

/// <summary>
/// Decides which IP addresses SiteHealth may connect to. Only addresses on the public internet are
/// allowed, so nobody can use our server to reach things that only it can reach (SSRF): the app
/// itself on localhost, machines on Azure's private network, or Azure's metadata service.
/// </summary>
public static class PublicAddressPolicy
{
    // Ranges that are not the public internet. IPNetwork.Contains does the CIDR maths for us.
    private static readonly IPNetwork[] Blocked =
    [
        // IPv4
        IPNetwork.Parse("0.0.0.0/8"),         // "this network"
        IPNetwork.Parse("10.0.0.0/8"),        // private
        IPNetwork.Parse("100.64.0.0/10"),     // carrier-grade NAT
        IPNetwork.Parse("127.0.0.0/8"),       // loopback (localhost)
        IPNetwork.Parse("169.254.0.0/16"),    // link-local, includes cloud metadata at 169.254.169.254
        IPNetwork.Parse("172.16.0.0/12"),     // private
        IPNetwork.Parse("192.0.0.0/24"),      // IETF protocol assignments
        IPNetwork.Parse("192.0.2.0/24"),      // documentation (TEST-NET-1)
        IPNetwork.Parse("192.168.0.0/16"),    // private
        IPNetwork.Parse("198.18.0.0/15"),     // benchmarking
        IPNetwork.Parse("198.51.100.0/24"),   // documentation (TEST-NET-2)
        IPNetwork.Parse("203.0.113.0/24"),    // documentation (TEST-NET-3)
        IPNetwork.Parse("224.0.0.0/4"),       // multicast
        IPNetwork.Parse("240.0.0.0/4"),       // reserved, includes broadcast 255.255.255.255
        IPNetwork.Parse("168.63.129.16/32"),  // Azure's internal host address: looks public, but isn't

        // IPv6
        IPNetwork.Parse("::/128"),            // unspecified
        IPNetwork.Parse("::1/128"),           // loopback
        IPNetwork.Parse("100::/64"),          // discard
        IPNetwork.Parse("2001:db8::/32"),     // documentation
        IPNetwork.Parse("fc00::/7"),          // unique local (IPv6's "private")
        IPNetwork.Parse("fe80::/10"),         // link-local
        IPNetwork.Parse("fec0::/10"),         // old site-local
        IPNetwork.Parse("ff00::/8"),          // multicast
    ];

    // IPv6 ranges that carry an IPv4 address inside them. We unwrap it and check that instead.
    private static readonly IPNetwork Nat64 = IPNetwork.Parse("64:ff9b::/96");   // IPv4 in the last 4 bytes
    private static readonly IPNetwork SixToFour = IPNetwork.Parse("2002::/16");  // IPv4 in bytes 2-5

    /// <summary>True when <paramref name="address"/> is on the public internet.</summary>
    public static bool IsPublic(IPAddress address)
    {
        // ::ffff:127.0.0.1 is just 127.0.0.1 written as IPv6
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            if (Nat64.Contains(address))
                return IsPublic(new IPAddress(bytes[12..16]));
            if (SixToFour.Contains(address))
                return IsPublic(new IPAddress(bytes[2..6]));
        }
        else if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        return !Blocked.Any(network => network.Contains(address));
    }

    /// <summary>
    /// A quick check on the URL itself, before any network request: true for "localhost" names and for
    /// private IP addresses typed straight into the URL. It can't catch a public name that resolves to a
    /// private address, or a redirect; <see cref="PublicNetworkConnector"/> catches those when connecting.
    /// </summary>
    public static bool IsObviouslyPrivate(Uri url)
    {
        // IsLoopback covers "localhost", 127.x.x.x and [::1]
        if (url.IsLoopback)
            return true;

        // DnsSafeHost gives "::1" rather than "[::1]" for IPv6
        if (IPAddress.TryParse(url.DnsSafeHost, out var address))
            return !IsPublic(address);

        return url.DnsSafeHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}
