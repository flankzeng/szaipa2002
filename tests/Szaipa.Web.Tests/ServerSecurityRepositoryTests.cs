using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class ServerSecurityRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-security-{Guid.NewGuid():N}");

    public ServerSecurityRepositoryTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private ServerSecurityRepository CreateRepository() =>
        new(new SecurityMonitoringPaths(
            ServerSecurityRoot: _root,
            AuditDirectory: Path.Combine(_root, "audit"),
            UnblockQueueDirectory: Path.Combine(_root, "unblock_queue"),
            WhitelistEditQueueDirectory: Path.Combine(_root, "whitelist_edit_queue"),
            RequestAuditEnabled: true,
            AuditKeepDays: 30,
            AuditMaxLineLength: 512,
            AuditSkipPaths: Array.Empty<string>()));

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void IsConfiguredReflectsRootExistence()
    {
        Assert.True(CreateRepository().IsConfigured);

        var missing = new ServerSecurityRepository(new SecurityMonitoringPaths(
            Path.Combine(_root, "missing"),
            Path.Combine(_root, "audit"),
            Path.Combine(_root, "unblock_queue"),
            Path.Combine(_root, "whitelist_edit_queue"),
            true, 30, 512, Array.Empty<string>()));
        Assert.False(missing.IsConfigured);
    }

    [Fact]
    public void ReadBlockedIpsParsesRowsAndSkipsHeaderAndMalformed()
    {
        WriteFile(Path.Combine(_root, "blocked_ips.csv"),
            "# IP,封禁时间戳\n203.0.113.9,1785818820\n\n198.51.100.77,garbage\n");

        var blocked = CreateRepository().ReadBlockedIps();

        Assert.Equal(2, blocked.Count);
        Assert.Equal("203.0.113.9", blocked[0].Ip);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1785818820), blocked[0].BlockedAtUtc);
        Assert.Equal("198.51.100.77", blocked[1].Ip);
        Assert.Null(blocked[1].BlockedAtUtc);
    }

    [Fact]
    public void ReadWhitelistSkipsCommentsAndBlanks()
    {
        WriteFile(Path.Combine(_root, "whitelist.txt"),
            "# 白名单\n157.120.59.35\n\n# 注释\n192.0.2.88\n");

        Assert.Equal(new[] { "157.120.59.35", "192.0.2.88" }, CreateRepository().ReadWhitelist());
    }

    [Fact]
    public void ReadAlertErrorsReturnsTail()
    {
        WriteFile(Path.Combine(_root, "alert_errors.log"),
            "line-1\nline-2\nline-3\nline-4\nline-5\n");

        var lines = CreateRepository().ReadAlertErrors(3);

        Assert.Equal(new[] { "line-3", "line-4", "line-5" }, lines);
    }

    [Fact]
    public void ReadSnapshotParsesJson()
    {
        WriteFile(Path.Combine(_root, "security_snapshot.json"),
            """
            {
              "schemaVersion": 1,
              "generatedAtUtc": "2026-08-04T05:00:00.0000000Z",
              "failedLogons24h": 1234,
              "lockouts24h": 5,
              "bySourceIp": [
                { "ip": "203.0.113.9", "failedLogons": 500, "users": ["administrator"], "lastSeenUtc": "2026-08-04T04:59:00.0000000Z" }
              ],
              "lockoutsByUser": [ { "user": "administrator", "count": 5 } ],
              "lastAlert": { "atUtc": "2026-08-04T04:50:00.0000000Z", "level": "Warning", "message": "暴力破解告警" }
            }
            """);

        var snapshot = CreateRepository().ReadSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal(1234, snapshot!.FailedLogons24h);
        Assert.Equal(5, snapshot.Lockouts24h);
        Assert.Single(snapshot.BySourceIp);
        Assert.Equal("203.0.113.9", snapshot.BySourceIp[0].Ip);
        Assert.Equal(500, snapshot.BySourceIp[0].FailedLogons);
        Assert.Equal(new[] { "administrator" }, snapshot.BySourceIp[0].Users);
        Assert.Equal("暴力破解告警", snapshot.LastAlert!.Message);
    }

    [Fact]
    public void ReadSnapshotReturnsNullWhenMissingOrMalformed()
    {
        Assert.Null(CreateRepository().ReadSnapshot());

        WriteFile(Path.Combine(_root, "security_snapshot.json"), "{ not-json ");
        Assert.Null(CreateRepository().ReadSnapshot());
    }

    [Fact]
    public void MissingFilesReturnEmptyCollections()
    {
        var repository = CreateRepository();

        Assert.Empty(repository.ReadBlockedIps());
        Assert.Empty(repository.ReadWhitelist());
        Assert.Empty(repository.ReadAlertErrors());
        Assert.Empty(repository.ReadQueueActions());
    }

    [Fact]
    public void ReadQueueActionsDistinguishesPendingAndProcessed()
    {
        Directory.CreateDirectory(Path.Combine(_root, "unblock_queue"));
        WriteFile(Path.Combine(_root, "unblock_queue", "203.0.113.9.txt"),
            "2026-08-04T04:55:00.0000000+08:00|admin");
        WriteFile(Path.Combine(_root, "unblock_queue", "203.0.113.5.done.txt"),
            "2026-08-04T05:00:00.0000000Z|ok|已删除规则");

        Directory.CreateDirectory(Path.Combine(_root, "whitelist_edit_queue"));
        WriteFile(Path.Combine(_root, "whitelist_edit_queue", "abc.txt"),
            "2026-08-04T05:01:00.0000000+08:00|admin|add|192.0.2.88");

        var actions = CreateRepository().ReadQueueActions();

        Assert.Equal(3, actions.Count);
        var unblock = Assert.Single(actions, a => a.Kind == QueueAction.KindUnblock && a.Status == QueueAction.StatusPending);
        Assert.Equal("203.0.113.9", unblock.TargetIp);
        Assert.Equal("admin", unblock.RequestedBy);
        var done = Assert.Single(actions, a => a.Status == QueueAction.StatusProcessed);
        Assert.Equal("203.0.113.5", done.TargetIp);
        Assert.Contains("已删除规则", done.Result!);
        var add = Assert.Single(actions, a => a.Kind == QueueAction.KindWhitelistAdd);
        Assert.Equal("192.0.2.88", add.TargetIp);
    }
}
