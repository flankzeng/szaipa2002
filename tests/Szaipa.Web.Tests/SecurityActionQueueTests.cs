using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SecurityActionQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-queue-{Guid.NewGuid():N}");

    public SecurityActionQueueTests()
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

    private SecurityActionQueue CreateQueue() =>
        new(new SecurityMonitoringPaths(
            ServerSecurityRoot: _root,
            AuditDirectory: Path.Combine(_root, "audit"),
            UnblockQueueDirectory: Path.Combine(_root, "unblock_queue"),
            WhitelistEditQueueDirectory: Path.Combine(_root, "whitelist_edit_queue"),
            RequestAuditEnabled: true,
            AuditKeepDays: 30,
            AuditMaxLineLength: 512,
            AuditSkipPaths: Array.Empty<string>()));

    private static readonly DateTimeOffset Fixed = new(2026, 8, 4, 5, 6, 7, TimeSpan.FromHours(8));

    [Fact]
    public void WriteUnblockCreatesIpNamedRequestFile()
    {
        var queue = CreateQueue();
        var ok = queue.WriteUnblock("203.0.113.9", "admin", Fixed);

        Assert.True(ok);
        var path = Path.Combine(_root, "unblock_queue", "203.0.113.9.txt");
        Assert.True(File.Exists(path));
        var body = File.ReadAllText(path);
        Assert.Contains("admin", body);
        Assert.Contains(Fixed.ToString("O"), body);
    }

    [Fact]
    public void WriteWhitelistEditWritesAddAndRemoveMarkers()
    {
        var queue = CreateQueue();

        Assert.True(queue.WriteWhitelistEdit("192.0.2.88", add: true, "admin", Fixed));
        Assert.True(queue.WriteWhitelistEdit("192.0.2.88", add: false, "admin", Fixed));

        // GetFiles order is not deterministic — assert by content, not by array index.
        var contents = Directory.GetFiles(Path.Combine(_root, "whitelist_edit_queue"), "*.txt")
            .Select(File.ReadAllText)
            .ToArray();
        Assert.Equal(2, contents.Length);
        Assert.Contains(contents, c => c.Contains("|add|192.0.2.88", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("|remove|192.0.2.88", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteFailsWhenQueueDirectoryCannotBeCreated()
    {
        // Make the security root a file so Directory.CreateDirectory on a child path fails.
        var blockingFile = Path.Combine(_root, "blocker");
        File.WriteAllText(blockingFile, "x");
        var queue = new SecurityActionQueue(new SecurityMonitoringPaths(
            ServerSecurityRoot: _root,
            AuditDirectory: Path.Combine(_root, "audit"),
            UnblockQueueDirectory: Path.Combine(blockingFile, "unblock_queue"),
            WhitelistEditQueueDirectory: Path.Combine(_root, "whitelist_edit_queue"),
            RequestAuditEnabled: true,
            AuditKeepDays: 30,
            AuditMaxLineLength: 512,
            AuditSkipPaths: Array.Empty<string>()));

        Assert.False(queue.WriteUnblock("203.0.113.9", "admin", Fixed));
    }

    [Fact]
    public void SanitizesRequestedByField()
    {
        var queue = CreateQueue();
        queue.WriteUnblock("203.0.113.9", "admin|injected\n", Fixed);

        // Exactly one line, and the requestedBy field holds the sanitized value (no embedded pipe/newline).
        var lines = File.ReadAllLines(Path.Combine(_root, "unblock_queue", "203.0.113.9.txt"));
        Assert.Single(lines);
        Assert.Equal($"{Fixed:O}|admin injected", lines[0]);
    }
}
