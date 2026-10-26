using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 艺术家「出版著作」（ArtistBook）端到端：后台仓储写入 -> 读上下文投影（同一 SQLite 连接），以及缺表降级。
/// 旧站占位 bug（Art 侧栏把 ArtNews 标题当书名）必须不再出现。
/// </summary>
public sealed class ArtistBookTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Admin_create_update_delete_preserves_cover_and_stamps_edit_record()
    {
        await using var fixture = CreateFixture();
        var repo = new ArtistBookAdminRepository(fixture.Admin, new OperationRecorder());

        var id = await repo.CreateAsync(
            new ArtistBook
            {
                ArtistId = 1,
                Title = "山水十讲",
                Publisher = "人民美术出版社",
                PublishYear = "2023",
                Description = "简介",
                Sort = 3,
                CoverPath = "c1.jpg"
            },
            Actor, CancellationToken.None);

        var created = await fixture.Admin.ArtistBook.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal("人民美术出版社", created.Publisher);
        Assert.Equal("2023", created.PublishYear);
        Assert.Equal(3, created.Sort);
        Assert.Equal("c1.jpg", created.CoverPath);
        Assert.Contains("新增了此出版著作", created.EditRecord);

        // 没有新封面的更新保留原封面。
        await repo.UpdateAsync(
            new ArtistBook { Id = id, ArtistId = 1, Title = "山水十讲（修订）", Publisher = "荣宝斋", PublishYear = "2024", Sort = 1, CoverPath = null },
            Actor, CancellationToken.None);
        var updated = await fixture.Admin.ArtistBook.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal("山水十讲（修订）", updated.Title);
        Assert.Equal("荣宝斋", updated.Publisher);
        Assert.Equal(1, updated.Sort);
        Assert.Equal("c1.jpg", updated.CoverPath);
        Assert.Contains("修改了此出版著作", updated.EditRecord);

        var page = await repo.GetPagedAsync(1, 10, CancellationToken.None, artistId: 1);
        Assert.Equal("张三", page.Items.Single().ArtistName);
        Assert.Empty((await repo.GetPagedAsync(1, 10, CancellationToken.None, artistId: 2)).Items);

        Assert.True(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Admin.ArtistBook.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Profile_side_panel_reads_books_by_sort_and_never_falls_back_to_art_news()
    {
        await using var fixture = CreateFixture();
        // ArtNews 有标题，但没有书：侧栏必须为空，而不是把资讯标题当书名。
        fixture.Admin.ArtNews.Add(new ArtNews { Id = 10, ArtistId = 1, Title = "一条资讯标题", SubTitle = "s" });
        fixture.Admin.SaveChanges();
        var reader = new ArtistReadRepository(fixture.Read);

        var empty = await reader.GetArtistProfileAsync(1);
        Assert.NotNull(empty);
        Assert.Empty(empty!.LatestPublications);

        var repo = new ArtistBookAdminRepository(fixture.Admin, new OperationRecorder());
        await repo.CreateAsync(new ArtistBook { ArtistId = 1, Title = "后排的书", Publisher = "乙社", Sort = 5 }, Actor, CancellationToken.None);
        await repo.CreateAsync(new ArtistBook { ArtistId = 1, Title = "排第一的书", Publisher = "甲社", Sort = 1, CoverPath = "b.jpg" }, Actor, CancellationToken.None);
        await repo.CreateAsync(new ArtistBook { ArtistId = 2, Title = "别人的书", Sort = 0 }, Actor, CancellationToken.None);

        var profile = await reader.GetArtistProfileAsync(1);

        var panel = Assert.Single(profile!.LatestPublications);
        Assert.Equal("排第一的书", panel.Title);
        Assert.Equal("甲社", panel.Subtitle);
        Assert.Equal("b.jpg", panel.CoverPath);
        // 其余侧栏不受影响：资讯依旧读 ArtNews。
        Assert.Equal("一条资讯标题", Assert.Single(profile.LatestNews).Title);
    }

    [Fact]
    public async Task Archive_lists_all_books_of_the_artist_ordered_by_sort_then_id()
    {
        await using var fixture = CreateFixture();
        var repo = new ArtistBookAdminRepository(fixture.Admin, new OperationRecorder());
        await repo.CreateAsync(new ArtistBook { ArtistId = 1, Title = "B", Sort = 2, PublishYear = "2020" }, Actor, CancellationToken.None);
        await repo.CreateAsync(new ArtistBook { ArtistId = 1, Title = "A1", Sort = 1, Description = "d" }, Actor, CancellationToken.None);
        await repo.CreateAsync(new ArtistBook { ArtistId = 1, Title = "A2", Sort = 1 }, Actor, CancellationToken.None);
        await repo.CreateAsync(new ArtistBook { ArtistId = 2, Title = "X", Sort = 0 }, Actor, CancellationToken.None);

        var archive = await new ArtistReadRepository(fixture.Read).GetArtistArchiveAsync(1);

        Assert.NotNull(archive);
        Assert.Equal(new[] { "A1", "A2", "B" }, archive!.Books.Select(b => b.Title).ToArray());
        Assert.Equal("d", archive.Books[0].Description);
        Assert.Equal("2020", archive.Books[2].PublishYear);
        // 可空列投影为空串，视图据此省略该行。
        Assert.Equal(string.Empty, archive.Books[1].Publisher);
        Assert.Equal(string.Empty, archive.Books[1].CoverPath);
    }

    [Fact]
    public async Task Missing_table_degrades_to_empty_without_throwing()
    {
        await using var fixture = CreateFixture();
        fixture.Admin.ArtNews.Add(new ArtNews { Id = 10, ArtistId = 1, Title = "一条资讯标题" });
        fixture.Admin.SaveChanges();
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE ArtistBook");
        var reader = new ArtistReadRepository(fixture.Read);

        var profile = await reader.GetArtistProfileAsync(1);
        var archive = await reader.GetArtistArchiveAsync(1);

        Assert.NotNull(profile);
        Assert.Empty(profile!.LatestPublications);
        Assert.NotEmpty(profile.LatestNews);
        Assert.NotNull(archive);
        Assert.Empty(archive!.Books);
        Assert.Single(archive.News);
    }

    [Fact]
    public async Task Site_overview_counts_books_and_flags_a_missing_table()
    {
        await using var fixture = CreateFixture();
        var books = new ArtistBookAdminRepository(fixture.Admin, new OperationRecorder());
        await books.CreateAsync(new ArtistBook { ArtistId = 1, Title = "一本书" }, Actor, CancellationToken.None);
        await books.CreateAsync(new ArtistBook { ArtistId = 1, Title = "两本书" }, Actor, CancellationToken.None);
        var site = new ArtistSiteAdminRepository(fixture.Admin, new OperationRecorder());

        var overview = await site.GetOverviewAsync(1, CancellationToken.None);
        Assert.Equal(2, overview!.BookCount);
        Assert.True(overview.BooksSupported);

        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE ArtistBook");
        var degraded = await site.GetOverviewAsync(1, CancellationToken.None);
        Assert.Equal(0, degraded!.BookCount);
        Assert.False(degraded.BooksSupported);
    }

    private static Fixture CreateFixture()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var admin = new SzaipaAdminContext(new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options);
        admin.Database.EnsureCreated();
        admin.Artist.Add(new Artist { Id = 1, ArtistNameCN = "张三" });
        admin.Artist.Add(new Artist { Id = 2, ArtistNameCN = "李四" });
        admin.SaveChanges();
        var read = new SzaipaLegacyReadContext(new DbContextOptionsBuilder<SzaipaLegacyReadContext>().UseSqlite(connection).Options);
        return new Fixture(admin, read, connection);
    }

    private sealed class Fixture(SzaipaAdminContext admin, SzaipaLegacyReadContext read, SqliteConnection connection) : IAsyncDisposable
    {
        public SzaipaAdminContext Admin { get; } = admin;

        public SzaipaLegacyReadContext Read { get; } = read;

        public async ValueTask DisposeAsync()
        {
            await Read.DisposeAsync();
            await Admin.DisposeAsync();
            connection.Dispose();
        }
    }
}
