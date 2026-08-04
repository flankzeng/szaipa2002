using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityAuditReaderTests : IDisposable
{
    private readonly string _auditDir = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-audit-reader-{Guid.NewGuid():N}");

    public SecurityAuditReaderTests()
    {
        Directory.CreateDirectory(_auditDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_auditDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private SecurityAuditReader CreateReader() =>
        new(new SecurityMonitoringPaths(
            ServerSecurityRoot: string.Empty,
            AuditDirectory: _auditDir,
            UnblockQueueDirectory: string.Empty,
            WhitelistEditQueueDirectory: string.Empty,
            RequestAuditEnabled: true,
            AuditKeepDays: 30,
            AuditMaxLineLength: 512,
            AuditSkipPaths: Array.Empty<string>()));

    [Fact]
    public void ReadRequestsParsesLinesAndSkipsMalformed()
    {
        var file = Path.Combine(_auditDir, $"requests-{DateTime.UtcNow:yyyyMMdd}.log");
        File.WriteAllText(file,
            "2026-08-04T05:06:07.0000000+08:00|203.0.113.5|GET|/Home/Index|200|12|Mozilla/5.0\n" +
            "malformed-line\n" +
            "2026-08-04T05:07:00.0000000+08:00|198.51.100.7|POST|/Staff/Account/Login|401|30|curl/8\n");

        var requests = CreateReader().ReadRequests(DateTimeOffset.Now.AddDays(-1));

        Assert.Equal(2, requests.Count);
        Assert.Equal("203.0.113.5", requests[0].Ip);
        Assert.Equal("/Home/Index", requests[0].Path);
        Assert.Equal(200, requests[0].Status);
        Assert.Equal(12, requests[0].DurationMs);
        Assert.Equal(401, requests[1].Status);
    }

    [Fact]
    public void ReadRequestsHonorsSinceCutoffByFileDate()
    {
        var oldFile = Path.Combine(_auditDir, "requests-20200101.log");
        File.WriteAllText(oldFile, "2026-08-04T05:06:07.0000000+08:00|203.0.113.5|GET|/x|200|1|ua\n");

        var requests = CreateReader().ReadRequests(DateTimeOffset.Now.AddDays(-1));

        Assert.Empty(requests);
    }

    [Fact]
    public void ReadLoginsReturnsNewestFirst()
    {
        File.WriteAllText(
            Path.Combine(_auditDir, "requests-20260803.log"),
            "ignored");
        File.WriteAllText(
            Path.Combine(_auditDir, "logins-20260803.log"),
            "2026-08-03T10:00:00.0000000+08:00|admin|203.0.113.5|0\n");
        File.WriteAllText(
            Path.Combine(_auditDir, "logins-20260804.log"),
            "2026-08-04T10:00:00.0000000+08:00|admin|203.0.113.5|1\n" +
            "2026-08-04T11:00:00.0000000+08:00|root|198.51.100.7|0\n");

        var logins = CreateReader().ReadLogins(10);

        // Newest file first: the 08-04 lines precede the 08-03 line.
        Assert.Equal(3, logins.Count);
        Assert.Equal("root", logins[0].Username);
        Assert.False(logins[0].Success);
        Assert.Equal("admin", logins[1].Username);
        Assert.True(logins[1].Success);
        Assert.False(logins[2].Success);
    }

    [Fact]
    public void ReadLoginsCapsAtMaxLines()
    {
        var lines = Enumerable.Range(0, 5)
            .Select(i => $"2026-08-04T10:00:0{i}.0000000+08:00|admin|203.0.113.5|1")
            .ToArray();
        File.WriteAllLines(
            Path.Combine(_auditDir, "logins-20260804.log"),
            lines);

        Assert.Equal(3, CreateReader().ReadLogins(3).Count);
    }

    [Fact]
    public void MissingAuditDirectoryYieldsEmpty()
    {
        var reader = new SecurityAuditReader(new SecurityMonitoringPaths(
            string.Empty,
            Path.Combine(_auditDir, "does-not-exist"),
            string.Empty,
            string.Empty,
            true, 30, 512, Array.Empty<string>()));

        Assert.Empty(reader.ReadRequests(DateTimeOffset.Now.AddDays(-1)));
        Assert.Empty(reader.ReadLogins(10));
    }
}
