using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.LiveEdit;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>现场编辑（T6）保存服务：白名单、规范化/清洗、站点文案 upsert、缺表提示。</summary>
public sealed class LiveEditServiceTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Whitelisted_field_is_saved_and_normalized_and_other_columns_are_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.News.Add(new News { Id = 7, Title = "旧标题", Content = "<p>旧</p>", CoverPath = "a.jpg" });
        await fixture.Db.SaveChangesAsync();

        var title = await fixture.Service.SaveFieldAsync("News", 7, "Title", "  新\n标题  ", Actor, default);
        Assert.True(title.Success);
        Assert.Equal("新 标题", title.Value); // 单行：换行折叠、去首尾空白

        var content = await fixture.Service.SaveFieldAsync("News", 7, "Content", "<p onclick=\"x()\">正文</p><script>alert(1)</script>", Actor, default);
        Assert.True(content.Success);
        Assert.DoesNotContain("script", content.Value);
        Assert.DoesNotContain("onclick", content.Value);

        // 不在白名单的列（封面路径）、未知实体、空必填项、超长都拒绝。
        Assert.False((await fixture.Service.SaveFieldAsync("News", 7, "CoverPath", "evil.jpg", Actor, default)).Success);
        Assert.False((await fixture.Service.SaveFieldAsync("Staff", 1, "Password", "x", Actor, default)).Success);
        Assert.False((await fixture.Service.SaveFieldAsync("News", 7, "Title", "   ", Actor, default)).Success);
        Assert.False((await fixture.Service.SaveFieldAsync("News", 7, "Title", new string('字', 201), Actor, default)).Success);
        Assert.False((await fixture.Service.SaveFieldAsync("News", 404, "Title", "x", Actor, default)).Success);

        var saved = await fixture.Db.News.AsNoTracking().SingleAsync(n => n.Id == 7);
        Assert.Equal("新 标题", saved.Title);
        Assert.Equal("a.jpg", saved.CoverPath);
    }

    [Fact]
    public async Task Site_text_is_upserted_keeps_line_breaks_and_invalidates_the_cache()
    {
        await using var fixture = await Fixture.CreateAsync();

        var first = await fixture.Service.SaveSiteTextAsync("home.core.title", "核心团队", LiveEditKind.Text, Actor, default);
        var second = await fixture.Service.SaveSiteTextAsync("home.core.title", "核心成员\n第二行", LiveEditKind.Lines, Actor, default);

        Assert.True(first.Success);
        Assert.Equal("核心成员\n第二行", second.Value);
        var row = await fixture.Db.SiteText.AsNoTracking().SingleAsync();
        Assert.Equal("核心成员\n第二行", row.Value);
        Assert.Equal("tester", row.UpdatedBy);
        Assert.Equal(2, fixture.Cache.Invalidations);

        Assert.False((await fixture.Service.SaveSiteTextAsync("Bad Key!", "x", LiveEditKind.Text, Actor, default)).Success);
        Assert.False((await fixture.Service.SaveSiteTextAsync("../etc", "x", LiveEditKind.Text, Actor, default)).Success);
    }

    [Fact]
    public async Task Missing_site_text_table_returns_a_friendly_error_instead_of_throwing()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TABLE SiteText;");

        var result = await fixture.Service.SaveSiteTextAsync("home.news.title", "新闻", LiveEditKind.Text, Actor, default);

        Assert.False(result.Success);
        Assert.Contains("SiteText", result.Error);
    }

    private sealed class CountingCache : ISiteTextProvider
    {
        public int Invalidations { get; private set; }

        public string? Get(string key) => null;

        public void Invalidate() => Invalidations++;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(SqliteConnection connection, SzaipaAdminContext db, CountingCache cache)
        {
            _connection = connection;
            Db = db;
            Cache = cache;
            Service = new LiveEditService(db, new OperationRecorder(), cache);
        }

        public SzaipaAdminContext Db { get; }

        public CountingCache Cache { get; }

        public LiveEditService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SzaipaAdminContext(new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.Staff.Add(new Szaipa.Data.Contexts.Szaipa.Staff { Id = 1, StaffName = "tester" });
            await db.SaveChangesAsync();
            return new Fixture(connection, db, new CountingCache());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
