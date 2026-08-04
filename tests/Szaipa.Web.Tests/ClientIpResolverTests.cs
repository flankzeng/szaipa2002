using System.Net;
using Microsoft.AspNetCore.Http;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class ClientIpResolverTests
{
    [Fact]
    public void FallsBackToConnectionAddressWithoutForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("203.0.113.7", ClientIpResolver.Resolve(context));
    }

    [Fact]
    public void PrefersFirstForwardedForHop()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.9, 10.0.0.5";

        Assert.Equal("198.51.100.9", ClientIpResolver.Resolve(context));
    }

    [Fact]
    public void IgnoresBlankForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        context.Request.Headers["X-Forwarded-For"] = "  ";

        Assert.Equal("203.0.113.7", ClientIpResolver.Resolve(context));
    }
}
