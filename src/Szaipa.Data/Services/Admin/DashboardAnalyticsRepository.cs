using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.IpGeolocation;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class DashboardAnalyticsRepository : IDashboardAnalyticsRepository
{
    // Legacy AccessData sentinel values that mean "no city" (see legacy visitcityM/ViustiySource).
    private static readonly string[] EmptyCityMarkers = { "None" };

    private readonly SzaipaAdminContext _db;
    private readonly IQqwryIpLocator _locator;

    public DashboardAnalyticsRepository(SzaipaAdminContext db, IQqwryIpLocator locator)
    {
        _db = db;
        _locator = locator;
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
        // 现场用 IP 定位省市：旧 Ctiy 字段在百度 IP 接口挂掉后全是空/垃圾值，直接按它聚合会得 0 行。
        // 按 (Ip, 存储省市) 在 SQL 侧分组，把行数收敛到唯一 IP 数再逐个定位 —— 客户端内存不随总行数膨胀。
        var grouped = await _db.AccessData.AsNoTracking()
            .Where(a => a.Date >= since)
            .GroupBy(a => new { a.Ip, a.Porvince, a.Ctiy })
            .Select(g => new { g.Key.Ip, g.Key.Porvince, g.Key.Ctiy, Count = (long)g.Count() })
            .ToListAsync(cancellationToken);

        var provinceTotal = new Dictionary<string, long>(StringComparer.Ordinal);
        var cityTotal = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);

        foreach (var row in grouped)
        {
            var (province, city) = ResolveRegion(row.Ip, row.Porvince, row.Ctiy);
            provinceTotal[province] = provinceTotal.GetValueOrDefault(province) + row.Count;
            if (string.IsNullOrEmpty(city)) continue; // 境外/未知等叶子：只记省汇总，不再分层
            if (!cityTotal.TryGetValue(province, out var cities))
            {
                cities = new Dictionary<string, long>(StringComparer.Ordinal);
                cityTotal[province] = cities;
            }
            cities[city] = cities.GetValueOrDefault(city) + row.Count;
        }

        return provinceTotal
            .Select(p => new GeoProvince
            {
                Name = p.Key,
                Value = p.Value,
                Children = cityTotal.TryGetValue(p.Key, out var cities)
                    ? cities.OrderByDescending(kv => kv.Value)
                        .Select(kv => new GeoCity(kv.Key, kv.Value))
                        .ToList()
                    : Array.Empty<GeoCity>()
            })
            .OrderByDescending(p => p.Value)
            .ToList();
    }

    /// <summary>
    /// 一行 AccessData → (省, 市)。优先 qqwry 按 IP 现场定位；定位不到（本机/局域网/坏 IP/文件缺失）时
    /// 回退到库里存的省市（百度接口还在时的好数据），仍无则归"未知"。市区空串表示叶子节点（境外/未知）。
    /// </summary>
    private (string Province, string City) ResolveRegion(string? ip, string? storedProvince, string? storedCity)
    {
        if (!string.IsNullOrWhiteSpace(ip))
        {
            var hit = _locator.Lookup(ip);
            if (hit is not null)
            {
                if (hit.IsChina)
                {
                    var province = string.IsNullOrEmpty(hit.Province) ? "未知" : hit.Province;
                    var city = string.IsNullOrEmpty(hit.City) ? province : hit.City;
                    return (province, city);
                }
                return ("境外访问", "");
            }
        }

        var pRaw = string.IsNullOrWhiteSpace(storedProvince) ? null : storedProvince.Trim();
        var c = string.IsNullOrWhiteSpace(storedCity) ? null : storedCity.Trim();
        if (pRaw is not null && pRaw != "境外访问")
        {
            // 旧数据里的省名可能是短名（"广东"），规范成标准名，避免与 qqwry 的"广东省"拆成两个节点。
            var p = IpRegion.NormalizeProvince(pRaw) ?? pRaw;
            return string.IsNullOrEmpty(c) || EmptyCityMarkers.Contains(c) ? (p, p) : (p, c);
        }
        return ("未知", "");
    }

    public async Task<IReadOnlyList<OperationRecordDay>> GetRecentOperationsAsync(int maxEntries, CancellationToken cancellationToken)
    {
        maxEntries = Math.Clamp(maxEntries, 1, 50);

        // 最近含操作记录的日记日（按日倒序）；上限 60 天足够收齐 50 条（每天至少 1 条）。
        var rows = await _db.Diary.AsNoTracking()
            .Where(d => d.Date != null && d.OperationRecord != null && d.OperationRecord != "")
            .OrderByDescending(d => d.Date)
            .Take(60)
            .Select(d => new { d.Date, d.OperationRecord })
            .ToListAsync(cancellationToken);

        // 取「最近 maxEntries 条」操作条目，而不是 maxEntries 天 —— 某天几十条会把整个列表撑爆。
        // 当天内记录按时间旧→新存储，所以从尾部倒着取最接近今天的新条目。
        var collected = new List<(DateTime Date, string Record)>(maxEntries);
        foreach (var row in rows)
        {
            if (!row.Date.HasValue) continue;
            var records = SplitRecords(row.OperationRecord);
            for (var i = records.Count - 1; i >= 0 && collected.Count < maxEntries; i--)
            {
                collected.Add((row.Date.Value, records[i]));
            }
            if (collected.Count >= maxEntries) break;
        }

        return collected
            .GroupBy(x => x.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new OperationRecordDay
            {
                Date = g.Key.ToString("yyyy年MM月dd日"),
                Records = g.Select(x => x.Record).Reverse().ToList() // 组内恢复时间旧→新
            })
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
