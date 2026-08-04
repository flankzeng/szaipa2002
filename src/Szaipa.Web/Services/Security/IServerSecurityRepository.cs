namespace Szaipa.Web.Services.Security;

/// <summary>
/// Read-only access to the server security tree (blocked_ips.csv, whitelist.txt, alert_errors.log,
/// security_snapshot.json and the queue directories). Never mutates these files — the SYSTEM scheduled
/// task is the only writer. All reads are bounded and tolerant of missing/malformed files.
/// </summary>
public interface IServerSecurityRepository
{
    /// <summary>True when the server security root exists and is readable.</summary>
    bool IsConfigured { get; }

    /// <summary>Parsed security_snapshot.json, or <c>null</c> when missing/unreadable/invalid.</summary>
    SecuritySnapshot? ReadSnapshot();

    IReadOnlyList<BlockedIp> ReadBlockedIps();

    IReadOnlyList<string> ReadWhitelist();

    /// <summary>Last <paramref name="maxLines"/> raw lines of alert_errors.log (best-effort tail).</summary>
    IReadOnlyList<string> ReadAlertErrors(int maxLines = 100);

    /// <summary>Pending and processed security actions from both queue directories, newest first.</summary>
    IReadOnlyList<QueueAction> ReadQueueActions();
}
