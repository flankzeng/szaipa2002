namespace Szaipa.Web.Configuration;

/// <summary>
/// Configuration for the staff security-monitoring dashboard (Areas/Staff SecurityMonitoring).
/// The web app only reads the server security files under <see cref="ServerSecurityRoot"/> and writes to
/// the two queue directories; a privileged SYSTEM scheduled task (scripts/security-snapshot.ps1) is the
/// only actor that mutates blocked_ips.csv / whitelist.txt / security_snapshot.json and runs netsh.
/// </summary>
public sealed class SecurityMonitoringOptions
{
    public const string SectionName = "SecurityMonitoring";

    /// <summary>
    /// Absolute path to the C:\Security tree: blocked_ips.csv, whitelist.txt, alert_errors.log,
    /// security_snapshot.json plus the unblock_queue / whitelist_edit_queue directories.
    /// Empty in Development falls back to a committed sample directory; required outside Development.
    /// </summary>
    public string ServerSecurityRoot { get; set; } = string.Empty;

    /// <summary>
    /// Master switch for the request-audit middleware and staff-login audit. Off by default (fail closed);
    /// production must opt in explicitly.
    /// </summary>
    public bool RequestAuditEnabled { get; set; }

    /// <summary>
    /// Root for the rolling requests-*.log / logins-*.log audit files. Empty derives a path from
    /// <see cref="ServerSecurityRoot"/> (production) or App_Data/security/audit (development).
    /// </summary>
    public string AuditDirectory { get; set; } = string.Empty;

    /// <summary>Delete audit files older than this many days.</summary>
    public int AuditKeepDays { get; set; } = 30;

    /// <summary>Cap for any single sanitized audit field, in characters.</summary>
    public int AuditMaxLineLength { get; set; } = 512;

    /// <summary>Path prefixes that are never written to the request audit log.</summary>
    public string[] AuditSkipPaths { get; set; } = new[] { "/healthz", "/favicon.ico" };

    /// <summary>
    /// Optional absolute override for the directory where staff unblock requests are queued.
    /// Empty derives &lt;ServerSecurityRoot&gt;\unblock_queue.
    /// </summary>
    public string UnblockQueueDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Optional absolute override for the directory where staff whitelist edit requests are queued.
    /// Empty derives &lt;ServerSecurityRoot&gt;\whitelist_edit_queue.
    /// </summary>
    public string WhitelistEditQueueDirectory { get; set; } = string.Empty;
}
