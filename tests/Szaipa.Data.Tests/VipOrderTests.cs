using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 会员风采（Vip）排序与显隐：后台仓储写 <c>ArtistDisplay</c> -> 前台读仓储（同一 SQLite 连接）。
/// 无记录的会员按原 Id 倒序排在有记录者之后；缺表时完全回退原顺序、不抛异常。
/// </summary>
public sealed class VipOrderTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Without_any_record_the_vip_list_keeps_the_original_id_descending_order()
    {
        await using var fixture = CreateFixture(5);

        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();

        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, vip.Select(a => a.Id).ToArray());
    }

    [Fact]
    public async Task Sort_ascending_wins_and_artists_without_a_record_follow_in_id_descending_order()
    {
        await using var fixture = CreateFixture(5);
        fixture.Admin.ArtistDisplay.AddRange(
            new ArtistDisplay { ArtistId = 1, Sort = 1 },
            new ArtistDisplay { ArtistId = 3, Sort = 2 });
        fixture.Admin.SaveChanges();

        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();

        // 有记录的 1、3 在前；5、4、2 没记录，保持 Id 倒序排在后面。
        Assert.Equal(new[] { 1, 3, 5, 4, 2 }, vip.Select(a => a.Id).ToArray());
    }

    [Fact]
    public async Task Hidden_artists_are_dropped_from_the_vip_list_but_still_exist()
    {
        await using var fixture = CreateFixture(3);
        fixture.Admin.ArtistDisplay.AddRange(
            new ArtistDisplay { ArtistId = 2, Sort = 1, IsHidden = true },
            new ArtistDisplay { ArtistId = 3, Sort = 2 });
        fixture.Admin.SaveChanges();
        var reader = new ArtistReadRepository(fixture.Read);

        var vip = await reader.GetVipArtistsAsync();

        Assert.Equal(new[] { 3, 1 }, vip.Select(a => a.Id).ToArray());
        // 艺术家官网页读的是 Artist 本身，隐藏不影响。
        Assert.NotNull(await reader.GetArtistByIdAsync(2));
    }

    [Fact]
    public async Task Missing_table_falls_back_to_the_original_order_without_throwing()
    {
        await using var fixture = CreateFixture(3);
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE ArtistDisplay");

        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();

        Assert.Equal(new[] { 3, 2, 1 }, vip.Select(a => a.Id).ToArray());
    }

    [Fact]
    public async Task Save_order_materializes_every_artist_and_is_reflected_on_the_public_list()
    {
        await using var fixture = CreateFixture(4);
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());

        // 请求里夹带不存在的 Id 与重复 Id，应被忽略；没提到的 4 保持原有相对顺序接在后面。
        await repo.SaveOrderAsync(new[] { 2, 99, 1, 2 }, Actor, CancellationToken.None);

        var rows = await fixture.Admin.ArtistDisplay.AsNoTracking().OrderBy(d => d.Sort).ToListAsync();
        Assert.Equal(new[] { 2, 1, 4, 3 }, rows.Select(r => r.ArtistId).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.Sort).ToArray());
        Assert.All(rows, r => Assert.False(r.IsHidden));

        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();
        Assert.Equal(new[] { 2, 1, 4, 3 }, vip.Select(a => a.Id).ToArray());
        Assert.Contains("会员风采", await fixture.Admin.Diary.Select(d => d.OperationRecord).SingleAsync());
    }

    [Fact]
    public async Task Save_order_preserves_hidden_flags()
    {
        await using var fixture = CreateFixture(3);
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());
        Assert.True(await repo.SetHiddenAsync(2, hidden: true, Actor, CancellationToken.None));

        await repo.SaveOrderAsync(new[] { 1, 2, 3 }, Actor, CancellationToken.None);

        var items = await repo.GetItemsAsync(CancellationToken.None);
        Assert.Equal(new[] { 1, 2, 3 }, items.Select(i => i.ArtistId).ToArray());
        Assert.Equal(new[] { false, true, false }, items.Select(i => i.IsHidden).ToArray());
        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();
        Assert.Equal(new[] { 1, 3 }, vip.Select(a => a.Id).ToArray());
    }

    [Fact]
    public async Task Hiding_and_unhiding_keeps_the_visible_order_stable()
    {
        await using var fixture = CreateFixture(4);
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());

        Assert.True(await repo.SetHiddenAsync(3, hidden: true, Actor, CancellationToken.None));
        var reader = new ArtistReadRepository(fixture.Read);
        Assert.Equal(new[] { 4, 2, 1 }, (await reader.GetVipArtistsAsync()).Select(a => a.Id).ToArray());

        Assert.True(await repo.SetHiddenAsync(3, hidden: false, Actor, CancellationToken.None));
        // 取消隐藏后回到原位，其余位置不漂移。
        Assert.Equal(new[] { 4, 3, 2, 1 }, (await reader.GetVipArtistsAsync()).Select(a => a.Id).ToArray());
        Assert.False(await repo.SetHiddenAsync(404, hidden: true, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task Showing_an_artist_that_has_no_record_writes_nothing()
    {
        await using var fixture = CreateFixture(2);
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());

        Assert.True(await repo.SetHiddenAsync(1, hidden: false, Actor, CancellationToken.None));

        Assert.Empty(await fixture.Admin.ArtistDisplay.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task New_artists_added_later_sort_after_the_curated_ones()
    {
        await using var fixture = CreateFixture(3);
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());
        await repo.SaveOrderAsync(new[] { 1, 2, 3 }, Actor, CancellationToken.None);

        fixture.Admin.Artist.Add(new Artist { Id = 10, ArtistNameCN = "新会员" });
        fixture.Admin.SaveChanges();

        var vip = await new ArtistReadRepository(fixture.Read).GetVipArtistsAsync();
        Assert.Equal(new[] { 1, 2, 3, 10 }, vip.Select(a => a.Id).ToArray());
        var items = await repo.GetItemsAsync(CancellationToken.None);
        Assert.Equal(10, items[^1].ArtistId);
    }

    [Fact]
    public async Task Admin_repository_surfaces_a_missing_table_as_a_database_exception()
    {
        await using var fixture = CreateFixture(2);
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE ArtistDisplay");
        var repo = new VipOrderAdminRepository(fixture.Admin, new OperationRecorder());

        var exception = await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(
            () => repo.GetItemsAsync(CancellationToken.None));

        Assert.True(Szaipa.Data.Services.SchemaCompatibility.IsMissingTable(exception, "ArtistDisplay"));
    }

    private static Fixture CreateFixture(int artistCount)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var admin = new SzaipaAdminContext(new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options);
        admin.Database.EnsureCreated();
        for (var id = 1; id <= artistCount; id++)
        {
            admin.Artist.Add(new Artist { Id = id, ArtistNameCN = $"会员{id}" });
        }

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
