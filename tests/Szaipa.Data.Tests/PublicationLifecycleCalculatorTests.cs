using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 展览状态自动流转：纯函数语义 + 与后台 SQL 筛选谓词（<c>Matches</c>）的一致性。
/// 固定「今天」= 2026-10-26，避免测试随日期漂移。
/// </summary>
public sealed class PublicationLifecycleCalculatorTests
{
    private static readonly DateTime Today = new(2026, 10, 26, 15, 30, 0); // 带时间部分，验证按天比较

    public static TheoryData<bool?, string?, string?, PublicationLifecycle> Cases => new()
    {
        // Status == null：下架，无论日期
        { null, "2026-10-01", "2026-11-30", PublicationLifecycle.Offline },
        { null, null, null, PublicationLifecycle.Offline },
        // Status == false：手动强制已结束，无论日期（slug 迁移的历史展览全是这个值）
        { false, "2026-10-01", "2026-11-30", PublicationLifecycle.Ended },
        { false, "2027-01-01", null, PublicationLifecycle.Ended },
        // Status == true：按日期自动流转
        { true, "2026-10-27", "2026-12-01", PublicationLifecycle.Upcoming },   // 明天开幕
        { true, "2026-10-26", "2026-12-01", PublicationLifecycle.Ongoing },    // 今天开幕
        { true, "2026-10-01", "2026-10-26", PublicationLifecycle.Ongoing },    // 今天闭幕（含当天）
        { true, "2026-10-01", "2026-10-25", PublicationLifecycle.Ended },      // 昨天闭幕
        { true, "2026-10-01", "2026-11-30", PublicationLifecycle.Ongoing },
        { true, "2024-06-28", null, PublicationLifecycle.Ongoing },            // 只有开幕日期（如 92011）：开幕后视为展览中
        { true, "2027-03-01", null, PublicationLifecycle.Upcoming },
        { true, null, "2026-12-01", PublicationLifecycle.Ongoing },            // 只有结束日期
        { true, null, "2026-10-25", PublicationLifecycle.Ended },
        { true, null, null, PublicationLifecycle.Ongoing },                    // 两个日期都空
        { true, "2026-12-01", "2026-10-01", PublicationLifecycle.Upcoming }    // 日期录反：以开幕日期为先
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Compute_follows_the_status_semantics(bool? status, string? start, string? end, PublicationLifecycle expected)
    {
        var actual = PublicationLifecycleCalculator.Compute(status, Parse(start), Parse(end), Today);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Compute_ignores_time_of_day_on_all_three_dates()
    {
        // 今天 00:00 / 23:59 的开幕与闭幕都按「当天」处理
        Assert.Equal(
            PublicationLifecycle.Ongoing,
            PublicationLifecycleCalculator.Compute(true, new DateTime(2026, 10, 26, 23, 59, 0), new DateTime(2026, 10, 26, 0, 0, 0), Today));
        Assert.Equal(
            PublicationLifecycle.Ongoing,
            PublicationLifecycleCalculator.Compute(true, new DateTime(2026, 10, 1), new DateTime(2026, 10, 26, 23, 59, 0), new DateTime(2026, 10, 26, 0, 0, 0)));
    }

    [Theory]
    [InlineData(PublicationLifecycle.Upcoming, "即将开幕")]
    [InlineData(PublicationLifecycle.Ongoing, "展览中")]
    [InlineData(PublicationLifecycle.Ended, "已结束")]
    [InlineData(PublicationLifecycle.Offline, "已下架")]
    public void Label_is_chinese_text(PublicationLifecycle lifecycle, string expected)
    {
        Assert.Equal(expected, PublicationLifecycleCalculator.Label(lifecycle));
    }

    [Fact]
    public async Task Matches_predicate_selects_exactly_the_rows_Compute_classifies()
    {
        await using var fixture = CreateContext();
        var id = 1;
        var rows = new List<(int Id, bool? Status, DateTime? Start, DateTime? End)>();
        foreach (var @case in Cases)
        {
            rows.Add((id++, (bool?)@case[0], Parse((string?)@case[1]), Parse((string?)@case[2])));
        }

        // 再补几行带时间部分的边界值（SQLite 以文本比较日期，时间部分不应影响按天结果）
        rows.Add((id++, true, new DateTime(2026, 10, 26, 23, 59, 59), new DateTime(2026, 10, 26, 23, 59, 59)));
        rows.Add((id++, true, new DateTime(2026, 10, 27, 0, 0, 0), null));
        rows.Add((id++, true, new DateTime(2026, 10, 1), new DateTime(2026, 10, 25, 23, 59, 59)));

        foreach (var row in rows)
        {
            fixture.Context.Database.ExecuteSqlRaw(
                "INSERT INTO Publication (Id, TitleCN, MaxImg, ReadCount, Status, StartDate, EndDate) VALUES (@id, 'x', 0, 0, @status, @start, @end)",
                new SqliteParameter("@id", row.Id),
                new SqliteParameter("@status", (object?)row.Status ?? DBNull.Value),
                new SqliteParameter("@start", row.Start?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value),
                new SqliteParameter("@end", row.End?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value));
        }

        foreach (var lifecycle in Enum.GetValues<PublicationLifecycle>())
        {
            var fromSql = await fixture.Context.Publication
                .AsNoTracking()
                .Where(PublicationLifecycleCalculator.Matches(lifecycle, Today))
                .Select(p => p.Id)
                .OrderBy(x => x)
                .ToListAsync();
            var expected = rows
                .Where(r => PublicationLifecycleCalculator.Compute(r.Status, r.Start, r.End, Today) == lifecycle)
                .Select(r => r.Id)
                .OrderBy(x => x)
                .ToList();

            Assert.Equal(expected, fromSql);
        }
    }

    private static DateTime? Parse(string? value) => value is null ? null : DateTime.Parse(value);

    private static Fixture CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options;
        var context = new SzaipaAdminContext(options);
        context.Database.EnsureCreated();
        return new Fixture(context, connection);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public Fixture(SzaipaAdminContext context, SqliteConnection connection)
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
