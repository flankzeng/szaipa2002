using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityIpPolicyTests
{
    [Theory]
    [InlineData("203.0.113.5")]
    [InlineData("192.168.1.1")]
    [InlineData("255.255.255.255")]
    [InlineData("1.1.1.1")]
    public void AcceptsPlainIpv4(string ip)
    {
        Assert.Equal(ip, SecurityIpPolicy.NormalizeIp(ip));
    }

    [Theory]
    [InlineData("203.0.113.5 ")]
    [InlineData(" 203.0.113.5")]
    [InlineData("203.0.113.5\n")]
    public void TrimsSurroundingWhitespace(string ip)
    {
        Assert.Equal("203.0.113.5", SecurityIpPolicy.NormalizeIp(ip));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    public void RejectsIpv6(string ip)
    {
        Assert.Null(SecurityIpPolicy.NormalizeIp(ip));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RejectsNullOrBlank(string? ip)
    {
        Assert.Null(SecurityIpPolicy.NormalizeIp(ip));
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("203.0.113.5/../../x")]
    [InlineData("203.0.113.5,8.8.8.8")]
    [InlineData("203.0.113.5abc")]
    [InlineData("203..113.5")]
    [InlineData("203.0.113")]
    [InlineData("a.b.c.d")]
    [InlineData("999.999.999.999")]
    [InlineData("203.0.113.5.6")]
    public void RejectsJunkAndTraversal(string ip)
    {
        Assert.Null(SecurityIpPolicy.NormalizeIp(ip));
    }

    [Fact]
    public void RejectsNonCanonicalLeadingZeros()
    {
        // Round-trips to 203.0.113.5 — the canonical form must match the input exactly.
        Assert.Null(SecurityIpPolicy.NormalizeIp("203.0.113.05"));
    }

    [Fact]
    public void RejectsPathsWithSeparatorSuffix()
    {
        Assert.Null(SecurityIpPolicy.NormalizeIp("203.0.113.5\\"));
    }
}
