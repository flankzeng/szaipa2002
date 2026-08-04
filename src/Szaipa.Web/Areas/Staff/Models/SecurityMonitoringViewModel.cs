namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Server-rendered part of the security-monitoring dashboard. Charts and tables load via AJAX.</summary>
public sealed class SecurityMonitoringViewModel
{
    /// <summary>False when the server security root is missing/unreadable — the view shows a hint instead.</summary>
    public bool DataAvailable { get; init; }

    public string? UnavailableReason { get; init; }

    /// <summary>Whether request/login auditing is enabled (SecurityMonitoring:RequestAuditEnabled).</summary>
    public bool RequestAuditEnabled { get; init; }

    public string? ServerSecurityRoot { get; init; }

    public DateTimeOffset? SnapshotGeneratedAtUtc { get; init; }
}
