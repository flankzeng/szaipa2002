namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Read-only analytics aggregations for the staff dashboard (legacy <c>StaffController.Index</c> charts:
/// DayVisityCount / DataCount / visitcityM / visitcityY / DieryTodayRecord). Queries the
/// <c>SzaipaAdminContext</c> with no-tracking — analytics never writes — so in production (admin context =
/// the live DB) it shows real visit data, and in a dev local copy it shows whatever that copy holds.
/// </summary>
public interface IDashboardAnalyticsRepository
{
    /// <summary>Headline KPI tiles.</summary>
    Task<DashboardKpi> GetKpiAsync(int recentOperationDays, CancellationToken cancellationToken);

    /// <summary>Per-day visit tallies for the last <paramref name="days"/> days, oldest→newest, zero-filled.</summary>
    Task<IReadOnlyList<DailyVisitPoint>> GetDailyVisitsAsync(int days, CancellationToken cancellationToken);

    /// <summary>Content-access totals: summed Works/Artist/Company VisitCount + News ReadCount.</summary>
    Task<IReadOnlyList<ContentAccessSlice>> GetContentAccessAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Province→city visit distribution from AccessData since <paramref name="since"/>, desc by count.
    /// Regions are resolved live from the visit IP via qqwry (legacy Ctiy/Porvince columns hold empty/foreign
    /// garbage since the Baidu lookup died); unresolvable IPs fall back to the stored values, else "未知".
    /// </summary>
    Task<IReadOnlyList<GeoProvince>> GetGeoDistributionAsync(DateTime since, CancellationToken cancellationToken);

    /// <summary>
    /// Most recent <paramref name="maxEntries"/> operation entries (not days — one busy day holds dozens),
    /// grouped by day newest-first, records chronological within a day.
    /// </summary>
    Task<IReadOnlyList<OperationRecordDay>> GetRecentOperationsAsync(int maxEntries, CancellationToken cancellationToken);

    /// <summary>Full operation-record history, paged by diary day and ordered newest first.</summary>
    Task<OperationRecordPage> GetOperationHistoryAsync(int page, int pageSize, CancellationToken cancellationToken);
}
