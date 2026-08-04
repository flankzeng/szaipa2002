using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class RequestAuditSkipPolicyTests
{
    private static readonly string[] DefaultSkipPrefixes = { "/healthz", "/favicon.ico" };

    [Theory]
    [InlineData("/Home/Index")]
    [InlineData("/Staff/SecurityMonitoring")]
    [InlineData("/api/data")]
    public void LogsDynamicPaths(string path)
    {
        Assert.False(RequestAuditSkipPolicy.ShouldSkip(path, DefaultSkipPrefixes));
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/favicon.ico")]
    public void SkipsConfiguredPrefixes(string path)
    {
        Assert.True(RequestAuditSkipPolicy.ShouldSkip(path, DefaultSkipPrefixes));
    }

    [Theory]
    [InlineData("/Content/images/logo.png")]
    [InlineData("/css/app.css")]
    [InlineData("/js/app.js")]
    [InlineData("/admin/dashboard.js")]
    [InlineData("/fonts/font.woff2")]
    public void SkipsStaticPathPrefixes(string path)
    {
        Assert.True(RequestAuditSkipPolicy.ShouldSkip(path, DefaultSkipPrefixes));
    }

    [Theory]
    [InlineData("/logo.png")]
    [InlineData("/photo.jpg")]
    [InlineData("/images.PNG")]
    [InlineData("/manifest.json")]
    public void SkipsStaticExtensions(string path)
    {
        Assert.True(RequestAuditSkipPolicy.ShouldSkip(path, DefaultSkipPrefixes));
    }

    [Fact]
    public void SkipsNullAndEmptyPaths()
    {
        Assert.True(RequestAuditSkipPolicy.ShouldSkip(null, DefaultSkipPrefixes));
        Assert.True(RequestAuditSkipPolicy.ShouldSkip(string.Empty, DefaultSkipPrefixes));
    }

    [Fact]
    public void PrefixMatchIsCaseInsensitive()
    {
        Assert.True(RequestAuditSkipPolicy.ShouldSkip("/HealthZ", DefaultSkipPrefixes));
        Assert.True(RequestAuditSkipPolicy.ShouldSkip("/content/images/x.png", DefaultSkipPrefixes));
    }
}
