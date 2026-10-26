using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>首页推荐展览仓储：排序（追加 / 上下移）、规范化与站内地址限制。</summary>
public sealed class FeaturedExhibitionAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_appends_at_the_end_when_sort_order_is_zero()
    {
        await using var fixture = CreateContext();
        var repository = new FeaturedExhibitionAdminRepository(fixture.Context, new OperationRecorder());

        var first = await repository.CreateAsync(New("一"), Actor, CancellationToken.None);
        var second = await repository.CreateAsync(New("二"), Actor, CancellationToken.None);
        var explicitSort = await repository.CreateAsync(New("置顶", sort: 5), Actor, CancellationToken.None);

        var all = await repository.GetAllAsync(CancellationToken.None);
        Assert.Equal(new[] { explicitSort, first, second }, all.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { 5, 10, 20 }, all.Select(x => x.SortOrder).ToArray());
    }

    [Fact]
    public async Task MoveAsync_swaps_neighbours_and_renumbers_even_with_tied_sort_values()
    {
        await using var fixture = CreateContext();
        var repository = new FeaturedExhibitionAdminRepository(fixture.Context, new OperationRecorder());
        var a = await repository.CreateAsync(New("A", sort: 7), Actor, CancellationToken.None);
        var b = await repository.CreateAsync(New("B", sort: 7), Actor, CancellationToken.None);
        var c = await repository.CreateAsync(New("C", sort: 7), Actor, CancellationToken.None);

        Assert.True(await repository.MoveAsync(c, -1, Actor, CancellationToken.None)); // A C B
        Assert.True(await repository.MoveAsync(a, 1, Actor, CancellationToken.None));  // C A B

        var all = await repository.GetAllAsync(CancellationToken.None);
        Assert.Equal(new[] { c, a, b }, all.Select(x => x.Id).ToArray());
        Assert.Equal(new[] { 10, 20, 30 }, all.Select(x => x.SortOrder).ToArray());
    }

    [Fact]
    public async Task MoveAsync_at_the_edges_is_a_noop_and_unknown_id_returns_false()
    {
        await using var fixture = CreateContext();
        var repository = new FeaturedExhibitionAdminRepository(fixture.Context, new OperationRecorder());
        var a = await repository.CreateAsync(New("A"), Actor, CancellationToken.None);
        var b = await repository.CreateAsync(New("B"), Actor, CancellationToken.None);

        Assert.True(await repository.MoveAsync(a, -1, Actor, CancellationToken.None));
        Assert.True(await repository.MoveAsync(b, 1, Actor, CancellationToken.None));
        Assert.False(await repository.MoveAsync(999, 1, Actor, CancellationToken.None));

        Assert.Equal(new[] { a, b }, (await repository.GetAllAsync(CancellationToken.None)).Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Save_normalises_text_and_refuses_off_site_addresses()
    {
        await using var fixture = CreateContext();
        var repository = new FeaturedExhibitionAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new FeaturedExhibition
            {
                Title = "  同构  ",
                YearText = " 2024 ",
                LinkUrl = "https://evil.example/",
                InfoLines = "展览时间：a \r\n\r\n  展览地点：b  ",
                ImageUrls = "/Content/a.jpg\nhttps://evil.example/x.jpg\n/Content/../b.jpg\n  /Content/c.jpg ",
                LogoUrl = "javascript:alert(1)",
                CoverUrl = "/Content/cover.jpg",
                IsVisible = true
            },
            Actor,
            CancellationToken.None);

        var row = (await repository.GetByIdAsync(id, CancellationToken.None))!;
        Assert.Equal("同构", row.Title);
        Assert.Equal("2024", row.YearText);
        Assert.Equal(string.Empty, row.LinkUrl);
        Assert.Equal("展览时间：a\n展览地点：b", row.InfoLines);
        Assert.Equal("/Content/a.jpg\n/Content/c.jpg", row.ImageUrls);
        Assert.Null(row.LogoUrl);
        Assert.Equal("/Content/cover.jpg", row.CoverUrl);
    }

    [Fact]
    public async Task Update_and_delete_work_and_report_missing_rows()
    {
        await using var fixture = CreateContext();
        var repository = new FeaturedExhibitionAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(New("旧名"), Actor, CancellationToken.None);

        var updated = New("新名");
        updated.Id = id;
        updated.IsVisible = false;
        updated.ShowInCarousel = true;
        updated.SortOrder = 3;
        Assert.True(await repository.UpdateAsync(updated, Actor, CancellationToken.None));
        Assert.False(await repository.UpdateAsync(new FeaturedExhibition { Id = 999, Title = "x", LinkUrl = "/x" }, Actor, CancellationToken.None));

        var row = (await repository.GetByIdAsync(id, CancellationToken.None))!;
        Assert.Equal("新名", row.Title);
        Assert.False(row.IsVisible);
        Assert.True(row.ShowInCarousel);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(id, CancellationToken.None));
    }

    private static FeaturedExhibition New(string title, int sort = 0) => new()
    {
        Title = title,
        LinkUrl = "/Home/Publication/92011",
        SortOrder = sort,
        IsVisible = true
    };

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
