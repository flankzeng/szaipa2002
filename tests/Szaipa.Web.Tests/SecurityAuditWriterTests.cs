using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityAuditWriterTests : IDisposable
{
    private readonly string _auditDir = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-audit-{Guid.NewGuid():N}");

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

    private SecurityAuditWriter CreateWriter(bool enabled = true, int keepDays = 30) =>
        new(
            new SecurityMonitoringPaths(
                ServerSecurityRoot: string.Empty,
                AuditDirectory: _auditDir,
                UnblockQueueDirectory: Path.Combine(_auditDir, "unblock_queue"),
                WhitelistEditQueueDirectory: Path.Combine(_auditDir, "whitelist_edit_queue"),
                RequestAuditEnabled: enabled,
                AuditKeepDays: keepDays,
                AuditMaxLineLength: 512,
                AuditSkipPaths: Array.Empty<string>()),
            Options.Create(new SecurityMonitoringOptions { RequestAuditEnabled = enabled }));

    [Fact]
    public void WritesRequestLineToDatedFile()
    {
        using var writer = CreateWriter();
        writer.WriteRequest("line-one");

        var file = Path.Combine(_auditDir, $"requests-{DateTime.UtcNow:yyyyMMdd}.log");
        Assert.True(File.Exists(file));
        Assert.Equal("line-one", File.ReadAllLines(file).Single());
    }

    [Fact]
    public void WritesLoginAttemptToDatedFile()
    {
        using var writer = CreateWriter();
        writer.WriteLoginAttempt("admin", "203.0.113.5", success: true);

        var file = Path.Combine(_auditDir, $"logins-{DateTime.UtcNow:yyyyMMdd}.log");
        Assert.True(File.Exists(file));
        var parts = File.ReadAllLines(file).Single().Split('|');
        Assert.Equal(4, parts.Length);
        Assert.Equal("admin", parts[1]);
        Assert.Equal("203.0.113.5", parts[2]);
        Assert.Equal("1", parts[3]);
    }

    [Fact]
    public void AppendsSubsequentLines()
    {
        using var writer = CreateWriter();
        writer.WriteRequest("a");
        writer.WriteRequest("b");
        writer.WriteRequest("c");

        var file = Path.Combine(_auditDir, $"requests-{DateTime.UtcNow:yyyyMMdd}.log");
        Assert.Equal(new[] { "a", "b", "c" }, File.ReadAllLines(file));
    }

    [Fact]
    public void SanitizesEmbeddedNewlinesOnWrite()
    {
        using var writer = CreateWriter();
        writer.WriteRequest("a\nb");

        var file = Path.Combine(_auditDir, $"requests-{DateTime.UtcNow:yyyyMMdd}.log");
        var lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.Equal("a b", lines[0]);
    }

    [Fact]
    public void DisabledWriterCreatesNothing()
    {
        using var writer = CreateWriter(enabled: false);
        writer.WriteRequest("x");
        writer.WriteLoginAttempt("admin", "1.2.3.4", true);

        Assert.False(Directory.Exists(_auditDir));
    }

    [Fact]
    public void PrunesFilesOlderThanKeepDays()
    {
        using var writer = CreateWriter(keepDays: 1);
        var oldFile = Path.Combine(_auditDir, "requests-20200101.log");
        Directory.CreateDirectory(_auditDir);
        File.WriteAllText(oldFile, "stale");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-2));

        writer.WriteRequest("fresh");

        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public void ConcurrentWritesAreNotLost()
    {
        const int count = 500;
        using var writer = CreateWriter();

        Parallel.For(0, count, i => writer.WriteRequest($"line-{i}"));

        var file = Path.Combine(_auditDir, $"requests-{DateTime.UtcNow:yyyyMMdd}.log");
        Assert.Equal(count, File.ReadAllLines(file).Length);
    }
}
