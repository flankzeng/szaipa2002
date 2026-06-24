using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
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
        var repo = new DashboardAnalyticsRepository(fixture.Context);

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
        var repo = new DashboardAnalyticsRepository(fixture.Context);

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
        var repo = new DashboardAnalyticsRepository(fixture.Context);

        var slices = await repo.GetContentAccessAsync(CancellationToken.None);

        Assert.All(slices, s => Assert.Equal(0, s.Value));
    }

    [Fact]
    public async Task GetGeoDistributionAsync_builds_province_city_tree_and_skips_empty_cities()
    {
        await using var fixture = CreateContext();
        var since = DateTime.Today.AddDays(-30);
        var d = DateTime.Today;
        fixture.Context.AccessData.AddRange(
            new AccessData { Date = d, Porvince = "广东", Ctiy = "深圳" },
            new AccessData { Date = d, Porvince = "广东", Ctiy = "深圳" },
            new AccessData { Date = d, Porvince = "广东", Ctiy = "广州" },
            new AccessData { Date = d, Porvince = "北京", Ctiy = "北京" },
            new AccessData { Date = d, Porvince = "广东", Ctiy = "None" }, // sentinel -> skipped
            new AccessData { Date = d, Porvince = "广东", Ctiy = "" },     // empty -> skipped
            new AccessData { Date = since.AddDays(-1), Porvince = "广东", Ctiy = "深圳" }); // out of window
        await fixture.Context.SaveChangesAsync();
        var repo = new DashboardAnalyticsRepository(fixture.Context);

        var provinces = await repo.GetGeoDistributionAsync(since, CancellationToken.None);

        var gd = provinces.Single(p => p.Name == "广东");
        Assert.Equal(3, gd.Value); // 2 深圳 + 1 广州 (None/empty/out-of-window excluded)
        Assert.Equal("深圳", gd.Children[0].Name); // desc by count
        Assert.Equal(2, gd.Children[0].Value);
        Assert.Equal(1, gd.Children.Single(c => c.Name == "广州").Value);
        Assert.Equal("广东", provinces[0].Name); // province desc by total
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
        var repo = new DashboardAnalyticsRepository(fixture.Context);

        var days = await repo.GetRecentOperationsAsync(7, CancellationToken.None);

        Assert.Equal(2, days.Count);
        Assert.Equal(today.ToString("yyyy年MM月dd日"), days[0].Date);
        Assert.Equal(new[] { "甲 编写了新闻", "乙 修改了作品" }, days[0].Records.ToArray());
        Assert.Single(days[1].Records);
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
        var repo = new DashboardAnalyticsRepository(fixture.Context);

        var kpi = await repo.GetKpiAsync(7, CancellationToken.None);

        Assert.Equal(12, kpi.TodayVisits);
        Assert.Equal(monthStart != today ? 20 : 12, kpi.MonthVisits);
        Assert.Equal(5, kpi.ContentAccessTotal);
        Assert.Equal(2, kpi.RecentOperationCount);
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
