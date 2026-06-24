using Szaipa.Data.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>Server-rendered part of the dashboard (KPI tiles + operation feed). Charts load via AJAX.</summary>
public sealed class DashboardViewModel
{
    /// <summary>False when the admin write DB is unconfigured or unreachable — the view shows a hint instead.</summary>
    public bool DataAvailable { get; init; }

    /// <summary>Human-readable reason shown when <see cref="DataAvailable"/> is false.</summary>
    public string? UnavailableReason { get; init; }

    public DashboardKpi Kpi { get; init; } = new();

    public IReadOnlyList<OperationRecordDay> Operations { get; init; } = Array.Empty<OperationRecordDay>();
}
