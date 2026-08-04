namespace Szaipa.Web.Infrastructure;

/// <summary>
/// Resolved, validated paths for the security-monitoring feature. Produced once at startup by
/// <see cref="SecurityMonitoringPathResolver"/> and injected as a singleton — no file-system I/O here.
/// </summary>
public sealed record SecurityMonitoringPaths(
    string ServerSecurityRoot,
    string AuditDirectory,
    string UnblockQueueDirectory,
    string WhitelistEditQueueDirectory,
    bool RequestAuditEnabled,
    int AuditKeepDays,
    int AuditMaxLineLength,
    IReadOnlyList<string> AuditSkipPaths);
