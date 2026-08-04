namespace Szaipa.Web.Services.Security;

/// <summary>An IP currently blocked by the AutoBlock firewall rules, as read from blocked_ips.csv.</summary>
public sealed record BlockedIp(string Ip, DateTimeOffset? BlockedAtUtc)
{
    /// <summary>Seconds remaining in the 24h block window; 0 when unknown or expired.</summary>
    public long RemainingSeconds
    {
        get
        {
            if (BlockedAtUtc is not { } blockedAt)
            {
                return 0;
            }

            var seconds = (blockedAt.AddHours(24) - DateTimeOffset.UtcNow).TotalSeconds;
            return seconds > 0 ? (long)seconds : 0;
        }
    }
}

/// <summary>Parsed security_snapshot.json contract — produced by scripts/security-snapshot.ps1 (SYSTEM task).</summary>
public sealed class SecuritySnapshot
{
    public int SchemaVersion { get; init; }

    public DateTimeOffset? GeneratedAtUtc { get; init; }

    public DateTimeOffset? WindowStartUtc { get; init; }

    public long FailedLogons24h { get; init; }

    public long Lockouts24h { get; init; }

    public IReadOnlyList<SourceIpAggregate> BySourceIp { get; init; } = Array.Empty<SourceIpAggregate>();

    public IReadOnlyList<UserLockoutAggregate> LockoutsByUser { get; init; } = Array.Empty<UserLockoutAggregate>();

    public LastAlertInfo? LastAlert { get; init; }
}

public sealed record SourceIpAggregate(
    string Ip,
    long FailedLogons,
    IReadOnlyList<string> Users,
    DateTimeOffset? LastSeenUtc);

public sealed record UserLockoutAggregate(string User, long Count);

public sealed record LastAlertInfo(DateTimeOffset? AtUtc, string? Level, string? Message);

/// <summary>
/// A staff-issued security action that was queued for the SYSTEM scheduled task to apply. Pending items
/// are raw *.txt request files; processed items are *.done.txt result files.
/// </summary>
public sealed record QueueAction(
    string FileName,
    string Kind,
    string? TargetIp,
    string Status,
    string? RequestedAtUtc,
    string? RequestedBy,
    string? Result,
    string? ProcessedAtUtc)
{
    public const string KindUnblock = "unblock";
    public const string KindWhitelistAdd = "whitelist-add";
    public const string KindWhitelistRemove = "whitelist-remove";
    public const string StatusPending = "pending";
    public const string StatusProcessed = "processed";
}
