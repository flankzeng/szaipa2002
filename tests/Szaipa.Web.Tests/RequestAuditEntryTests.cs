using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class RequestAuditEntryTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 8, 4, 5, 6, 7, TimeSpan.FromHours(8));

    [Fact]
    public void FormatProducesPipeDelimitedLine()
    {
        var line = RequestAuditEntry.Format(Fixed, "203.0.113.5", "GET", "/Home/Index", 200, 12, "Mozilla/5.0");

        var parts = line.Split('|');
        Assert.Equal(7, parts.Length);
        Assert.Equal("2026-08-04T05:06:07.0000000+08:00", parts[0]);
        Assert.Equal("203.0.113.5", parts[1]);
        Assert.Equal("GET", parts[2]);
        Assert.Equal("/Home/Index", parts[3]);
        Assert.Equal("200", parts[4]);
        Assert.Equal("12", parts[5]);
        Assert.Equal("Mozilla/5.0", parts[6]);
    }

    [Fact]
    public void FormatLoginProducesPipeDelimitedLine()
    {
        var line = RequestAuditEntry.FormatLogin(Fixed, "admin", "203.0.113.5", success: true);

        var parts = line.Split('|');
        Assert.Equal(4, parts.Length);
        Assert.Equal("admin", parts[1]);
        Assert.Equal("203.0.113.5", parts[2]);
        Assert.Equal("1", parts[3]);
    }

    [Fact]
    public void SanitizeStripsCrLfAndPipe()
    {
        var result = RequestAuditEntry.Sanitize("a\nb| c\r d", 512);

        Assert.Equal("a b  c  d", result);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('|', result);
    }

    [Fact]
    public void SanitizeCapsLength()
    {
        var result = RequestAuditEntry.Sanitize(new string('x', 100), 16);

        Assert.Equal(16, result.Length);
        Assert.Equal(new string('x', 16), result);
    }

    [Fact]
    public void FormatNeutralizesPipeInUserAgent()
    {
        var line = RequestAuditEntry.Format(Fixed, "203.0.113.5", "GET", "/p", 200, 1, "evil|agent|field");

        // The user-agent field's pipes are neutralized so the line still has exactly 7 fields.
        Assert.Equal(7, line.Split('|').Length);
        Assert.Contains("evil agent field", line);
    }
}
