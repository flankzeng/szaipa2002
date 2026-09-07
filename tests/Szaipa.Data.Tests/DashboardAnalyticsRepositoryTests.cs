using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.IpGeolocation;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the read-only dashboard analytics aggregations over a SQLite in-memory admin database: daily
/// visit zero-filling, content-access sums, the province→city geo tree, operation-record splitting, and KPIs.
/// Data is seeded relative to DateTime.Today so the date-window logic is exercised deterministically.
/// </summary>
public sealed class DashboardAnalyticsRepositoryTests
{
    [Fact]
    public async Task GetDailyVisitsAsync_zero_fills_missing_days_and_orders_oldest_first()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        fixture.Context.Diary.Add(new Diary { Date = today, VistiTotal = 10 });
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-2), VistiTotal = 5 });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var points = await repo.GetDailyVisitsAsync(3, CancellationToken.None);

        Assert.Equal(3, points.Count);
        Assert.Equal(5, points[0].Count);  // today-2
        Assert.Equal(0, points[1].Count);  // today-1 (missing -> 0)
        Assert.Equal(10, points[2].Count); // today
        Assert.Equal(today.ToString("MM.dd"), points[2].Date);
    }

    [Fact]
    public async Task GetContentAccessAsync_sums_each_content_type()
    {
        await using var fixture = CreateContext();
        fixture.Context.Works.Add(new Works { ArtistId = 1, Title = "w1", VisitCount = 3 });
        fixture.Context.Works.Add(new Works { ArtistId = 1, Title = "w2", VisitCount = 4 });
        fixture.Context.Artist.Add(new Artist { ArtistNameCN = "a", VisitCount = 100 });
        fixture.Context.Company.Add(new Company { NameCN = "c", VisitCount = 50 });
        fixture.Context.News.Add(new News { Title = "n", ReadCount = 7 });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var slices = await repo.GetContentAccessAsync(CancellationToken.None);

        Assert.Equal(7, slices.Single(s => s.Name == "访问作品").Value);
        Assert.Equal(100, slices.Single(s => s.Name == "访问会员").Value);
        Assert.Equal(50, slices.Single(s => s.Name == "访问企业").Value);
        Assert.Equal(7, slices.Single(s => s.Name == "新闻阅读数").Value);
    }

    [Fact]
    public async Task GetContentAccessAsync_returns_zeros_on_empty_tables()
    {
        await using var fixture = CreateContext();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var slices = await repo.GetContentAccessAsync(CancellationToken.None);

        Assert.All(slices, s => Assert.Equal(0, s.Value));
    }

    [Fact]
    public async Task GetGeoDistributionAsync_resolves_by_ip_with_stored_fallback()
    {
        await using var fixture = CreateContext();
        var since = DateTime.Today.AddDays(-30);
        var d = DateTime.Today;
        fixture.Context.AccessData.AddRange(
            new AccessData { Date = d, Ip = "120.78.90.25" },                          // qqwry -> 广东省/深圳
            new AccessData { Date = d, Ip = "120.78.90.25" },                          // qqwry -> 广东省/深圳
            new AccessData { Date = d, Ip = "61.144.0.1" },                            // qqwry -> 广东省/广州
            new AccessData { Date = d, Ip = "8.8.8.8" },                               // qqwry -> 境外访问
            new AccessData { Date = d, Ip = null, Porvince = "广东", Ctiy = "惠州" },   // fallback -> 广东省/惠州
            new AccessData { Date = d, Ip = null, Porvince = "境外访问", Ctiy = "" },  // no usable -> 未知
            new AccessData { Date = since.AddDays(-1), Ip = "120.78.90.25" });         // out of window
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, CreateGeoLocator());

        var provinces = await repo.GetGeoDistributionAsync(since, CancellationToken.None);

        var gd = provinces.Single(p => p.Name == "广东省");
        Assert.Equal(4, gd.Value); // 2 深圳 + 1 广州 + 1 惠州（回退，省名短名规范化为标准名）
        Assert.Equal("深圳", gd.Children[0].Name); // desc by count
        Assert.Equal(2, gd.Children[0].Value);
        Assert.Equal(1, gd.Children.Single(c => c.Name == "广州").Value);
        Assert.Equal(1, gd.Children.Single(c => c.Name == "惠州").Value);

        Assert.Equal(1, provinces.Single(p => p.Name == "境外访问").Value);
        Assert.Empty(provinces.Single(p => p.Name == "境外访问").Children); // 叶子，不再分层
        Assert.Equal(1, provinces.Single(p => p.Name == "未知").Value);
        Assert.Equal("广东省", provinces[0].Name); // province desc by total (4 > 1)
    }

    [Fact]
    public async Task GetRecentOperationsAsync_splits_records_and_orders_newest_first()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        fixture.Context.Diary.Add(new Diary { Date = today, OperationRecord = "甲 编写了新闻/乙 修改了作品/" });
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-1), OperationRecord = "丙 删除了展览/" });
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-2), OperationRecord = "" }); // empty -> excluded
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var days = await repo.GetRecentOperationsAsync(7, CancellationToken.None);

        Assert.Equal(2, days.Count);
        Assert.Equal(today.ToString("yyyy年MM月dd日"), days[0].Date);
        Assert.Equal(new[] { "甲 编写了新闻", "乙 修改了作品" }, days[0].Records.ToArray());
        Assert.Single(days[1].Records);
    }

    [Fact]
    public async Task GetRecentOperationsAsync_caps_total_entries_not_days()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        // 今天 6 条 + 昨天 4 条 = 恰好 10 条封顶；前天 3 条不应出现。
        fixture.Context.Diary.Add(new Diary { Date = today, OperationRecord = "1A/1B/1C/1D/1E/1F/" });
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-1), OperationRecord = "2A/2B/2C/2D/" });
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-2), OperationRecord = "3A/3B/3C/" });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var days = await repo.GetRecentOperationsAsync(10, CancellationToken.None);

        Assert.Equal(2, days.Count);
        Assert.Equal(10, days.Sum(d => d.Records.Count));
        Assert.Equal(today.ToString("yyyy年MM月dd日"), days[0].Date);
        Assert.Equal(new[] { "1A", "1B", "1C", "1D", "1E", "1F" }, days[0].Records.ToArray());
        Assert.Equal(new[] { "2A", "2B", "2C", "2D" }, days[1].Records.ToArray());
    }

    [Fact]
    public async Task GetRecentOperationsAsync_folds_single_busy_day()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        fixture.Context.Diary.Add(new Diary { Date = today, OperationRecord = "a/b/c/d/e/" });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var days = await repo.GetRecentOperationsAsync(3, CancellationToken.None);

        Assert.Single(days);
        Assert.Equal(new[] { "c", "d", "e" }, days[0].Records.ToArray()); // 单日 5 条只取最新 3 条
    }

    [Fact]
    public async Task GetOperationHistoryAsync_pages_days_newest_first()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        for (var i = 0; i < 12; i++)
        {
            fixture.Context.Diary.Add(new Diary
            {
                Date = today.AddDays(-i),
                OperationRecord = $"操作{i}A/操作{i}B/"
            });
        }
        fixture.Context.Diary.Add(new Diary { Date = today.AddDays(-20), OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var result = await repo.GetOperationHistoryAsync(2, 5, CancellationToken.None);

        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(12, result.TotalDays);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(today.AddDays(-5).ToString("yyyy年MM月dd日"), result.Items[0].Date);
        Assert.Equal(new[] { "操作5A", "操作5B" }, result.Items[0].Records);
    }

    [Fact]
    public async Task GetOperationHistoryAsync_clamps_page_to_last_page()
    {
        await using var fixture = CreateContext();
        fixture.Context.Diary.AddRange(
            new Diary { Date = DateTime.Today, OperationRecord = "今天/" },
            new Diary { Date = DateTime.Today.AddDays(-1), OperationRecord = "昨天/" });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var result = await repo.GetOperationHistoryAsync(99, 5, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.TotalDays);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetKpiAsync_aggregates_today_month_content_and_recent_ops()
    {
        await using var fixture = CreateContext();
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        fixture.Context.Diary.Add(new Diary { Date = today, VistiTotal = 12, OperationRecord = "甲 操作1/甲 操作2/" });
        // Another in-month day (use month start, always within this month and <= today).
        if (monthStart != today)
        {
            fixture.Context.Diary.Add(new Diary { Date = monthStart, VistiTotal = 8 });
        }
        fixture.Context.Works.Add(new Works { ArtistId = 1, Title = "w", VisitCount = 5 });
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context, EmptyLocator());

        var kpi = await repo.GetKpiAsync(7, CancellationToken.None);

        Assert.Equal(12, kpi.TodayVisits);
        Assert.Equal(monthStart != today ? 20 : 12, kpi.MonthVisits);
        Assert.Equal(5, kpi.ContentAccessTotal);
        Assert.Equal(2, kpi.RecentOperationCount);
    }

    /// <summary>空定位器：任何 IP 都查不到 → 全走存储省市回退 / "未知"。</summary>
    private static IQqwryIpLocator EmptyLocator() =>
        new StubQqwryLocator(new Dictionary<string, IpRegion.Result>());

    /// <summary>带少量映射的定位器，验证 IP 现场解析路径。</summary>
    private static IQqwryIpLocator CreateGeoLocator() => new StubQqwryLocator(new Dictionary<string, IpRegion.Result>
    {
        ["120.78.90.25"] = new IpRegion.Result { IsChina = true, Province = "广东省", City = "深圳" },
        ["61.144.0.1"] = new IpRegion.Result { IsChina = true, Province = "广东省", City = "广州" },
        ["8.8.8.8"] = new IpRegion.Result { IsChina = false, Nation = "美国" }
    });

    private sealed class StubQqwryLocator : IQqwryIpLocator
    {
        private readonly IReadOnlyDictionary<string, IpRegion.Result> _map;

        public StubQqwryLocator(IReadOnlyDictionary<string, IpRegion.Result> map) => _map = map;

        public bool IsAvailable => true;

        public IpRegion.Result? Lookup(string? ip) =>
            string.IsNullOrEmpty(ip) ? null : (_map.TryGetValue(ip!, out var r) ? r : null);
    }

    private static AdminContextFixture CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SzaipaAdminContext>()
            .UseSqlite(connection)
            .Options;
        var context = new SzaipaAdminContext(options);
        context.Database.EnsureCreated();
        return new AdminContextFixture(context, connection);
    }

    private sealed class AdminContextFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public AdminContextFixture(SzaipaAdminContext context, SqliteConnection connection)
        {
            Context = context;
            _connection = connection;
        }

        public SzaipaAdminContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _connection.Dispose();
        }
    }
}
