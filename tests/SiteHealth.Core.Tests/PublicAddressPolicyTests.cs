using System.Net;
using SiteHealth.Core.Security;

namespace SiteHealth.Core.Tests;

public class PublicAddressPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("::ffff:8.8.8.8")]          // public IPv4 written as IPv6
    [InlineData("64:ff9b::808:808")]        // NAT64 wrapping 8.8.8.8
    [InlineData("2002:808:808::1")]         // 6to4 wrapping 8.8.8.8
    public void Allows_public_addresses(string address)
    {
        Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("169.254.169.254")]         // cloud metadata service
    [InlineData("168.63.129.16")]           // Azure's internal host address
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.0.2.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:127.0.0.1")]        // localhost written as IPv6
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]         // NAT64 wrapping 127.0.0.1
    [InlineData("2002:a00:1::1")]           // 6to4 wrapping 10.0.0.1
    public void Blocks_private_and_reserved_addresses(string address)
    {
        Assert.False(PublicAddressPolicy.IsPublic(IPAddress.Parse(address)));
    }

    [Fact]
    public void Allows_the_edges_just_outside_private_ranges()
    {
        Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse("172.15.255.255")));
        Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse("172.32.0.0")));
        Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse("100.63.255.255")));
        Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse("11.0.0.0")));
    }

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST:5080/scans")]
    [InlineData("http://app.localhost/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::ffff:192.168.0.1]/")]
    [InlineData("http://2130706433/")]       // 127.0.0.1 written as one number
    public void Spots_obviously_private_urls(string url)
    {
        Assert.True(PublicAddressPolicy.IsObviouslyPrivate(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://8.8.8.8/")]
    [InlineData("https://localhost.example.com/")]   // only a real "localhost" name counts
    public void Lets_public_looking_urls_through(string url)
    {
        Assert.False(PublicAddressPolicy.IsObviouslyPrivate(new Uri(url)));
    }
}
