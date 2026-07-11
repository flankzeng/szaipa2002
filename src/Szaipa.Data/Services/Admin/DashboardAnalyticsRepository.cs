using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class DashboardAnalyticsRepository : IDashboardAnalyticsRepository
{
    // Legacy AccessData sentinel values that mean "no city" (see legacy visitcityM/ViustiySource).
    private static readonly string[] EmptyCityMarkers = { "None" };

    private readonly SzaipaAdminContext _db;

    public DashboardAnalyticsRepository(SzaipaAdminContext db)
    {
        _db = db;
    }

    public async Task<DashboardKpi> GetKpiAsync(int recentOperationDays, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var todayVisits = await _db.Diary.AsNoTracking()
            .Where(d => d.Date == today)
            .Select(d => (int?)d.VistiTotal)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var monthVisits = await _db.Diary.AsNoTracking()
            .Where(d => d.Date >= monthStart)
            .SumAsync(d => (long?)d.VistiTotal, cancellationToken) ?? 0;

        var contentSlices = await GetContentAccessAsync(cancellationToken);
        var contentTotal = contentSlices.Sum(s => s.Value);

        var since = today.AddDays(-(Math.Max(1, recentOperationDays) - 1));
        var recentOps = await _db.Diary.AsNoTracking()
            .Where(d => d.Date >= since && d.OperationRecord != null && d.OperationRecord != "")
            .Select(d => d.OperationRecord!)
            .ToListAsync(cancellationToken);
        var recentOpCount = recentOps.Sum(r => SplitRecords(r).Count);

        return new DashboardKpi
        {
            TodayVisits = todayVisits,
            MonthVisits = monthVisits,
            ContentAccessTotal = contentTotal,
            RecentOperationCount = recentOpCount
        };
    }

    public async Task<IReadOnlyList<DailyVisitPoint>> GetDailyVisitsAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 365);
        var today = DateTime.Today;
        var since = today.AddDays(-(days - 1));

        // One query for the window, then zero-fill missing days in memory (Diary has at most one row/day).
        var tallies = await _db.Diary.AsNoTracking()
            .Where(d => d.Date >= since && d.Date <= today)
            .Select(d => new { d.Date, d.VistiTotal })
            .ToListAsync(cancellationToken);

        var byDate = tallies
            .Where(t => t.Date.HasValue)
            .GroupBy(t => t.Date!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.VistiTotal));

        var points = new List<DailyVisitPoint>(days);
        for (var i = 0; i < days; i++)
        {
            var date = since.AddDays(i);
            points.Add(new DailyVisitPoint(
                date.ToString("MM.dd"),
                byDate.TryGetValue(date, out var count) ? count : 0));
        }

        return points;
    }

    public async Task<IReadOnlyList<ContentAccessSlice>> GetContentAccessAsync(CancellationToken cancellationToken)
    {
        var works = await _db.Works.AsNoTracking().SumAsync(w => (long?)w.VisitCount, cancellationToken) ?? 0;
        var artists = await _db.Artist.AsNoTracking().SumAsync(a => (long?)a.VisitCount, cancellationToken) ?? 0;
        var companies = await _db.Company.AsNoTracking().SumAsync(c => (long?)c.VisitCount, cancellationToken) ?? 0;
        var news = await _db.News.AsNoTracking().SumAsync(n => (long?)n.ReadCount, cancellationToken) ?? 0;

        return new[]
        {
            new ContentAccessSlice("访问作品", works),
            new ContentAccessSlice("访问会员", artists),
            new ContentAccessSlice("访问企业", companies),
            new ContentAccessSlice("新闻阅读数", news)
        };
    }

    public async Task<IReadOnlyList<GeoProvince>> GetGeoDistributionAsync(DateTime since, CancellationToken cancellationToken)
    {
        // Aggregate by (province, city) in SQL, then assemble the province→city tree in memory.
        var grouped = await _db.AccessData.AsNoTracking()
            .Where(a => a.Date >= since
                && a.Ctiy != null
                && a.Ctiy != ""
                && !EmptyCityMarkers.Contains(a.Ctiy))
            .GroupBy(a => new { a.Porvince, a.Ctiy })
            .Select(g => new { g.Key.Porvince, g.Key.Ctiy, Count = (long)g.Count() })
            .ToListAsync(cancellationToken);

        return grouped
            .GroupBy(g => g.Porvince ?? "未知")
            .Select(provinceGroup => new GeoProvince
            {
                Name = provinceGroup.Key,
                Value = provinceGroup.Sum(x => x.Count),
                Children = provinceGroup
                    .OrderByDescending(x => x.Count)
                    .Select(x => new GeoCity(x.Ctiy!, x.Count))
                    .ToList()
            })
            .OrderByDescending(p => p.Value)
            .ToList();
    }

    public async Task<IReadOnlyList<OperationRecordDay>> GetRecentOperationsAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 90);
        var today = DateTime.Today;
        var since = today.AddDays(-(days - 1));

        var rows = await _db.Diary.AsNoTracking()
            .Where(d => d.Date >= since && d.Date <= today)
            .Select(d => new { d.Date, d.OperationRecord })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Date.HasValue)
            .OrderByDescending(r => r.Date!.Value)
            .Select(r => new OperationRecordDay
            {
                Date = r.Date!.Value.ToString("yyyy年MM月dd日"),
                Records = SplitRecords(r.OperationRecord)
            })
            .Where(d => d.Records.Count > 0)
            .ToList();
    }

    public async Task<OperationRecordPage> GetOperationHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var query = _db.Diary.AsNoTracking()
            .Where(d => d.Date != null && d.OperationRecord != null && d.OperationRecord != "");
        var totalDays = await query.CountAsync(cancellationToken);
        var totalPages = totalDays == 0 ? 0 : (int)Math.Ceiling((double)totalDays / pageSize);
        if (totalPages > 0)
        {
            page = Math.Min(page, totalPages);
        }

        var rows = await query
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new { d.Date, d.OperationRecord })
            .ToListAsync(cancellationToken);

        return new OperationRecordPage
        {
            Items = rows.Select(r => new OperationRecordDay
            {
                Date = r.Date!.Value.ToString("yyyy年MM月dd日"),
                Records = SplitRecords(r.OperationRecord)
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalDays = totalDays
        };
    }

    private static List<string> SplitRecords(string? operationRecord) =>
        string.IsNullOrEmpty(operationRecord)
            ? new List<string>()
            : operationRecord
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
}
