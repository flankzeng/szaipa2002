using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Exercises the generic <see cref="ArtistScopedAdminRepository{T}"/> through its concrete derivations
/// (Exhibition/Fav/Auction): create/update/delete with operation logging, cover-preserve-on-empty, and the
/// artist-name join in the list.
/// </summary>
public sealed class ArtistScopedAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Exhibition_create_update_delete_and_list_join()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 5, "李四");
        var repo = new ExhibitionAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repo.CreateAsync(
            new Exhibition { ArtistId = 5, Title = "个展", Location = "北京", CoverPath = "c1.jpg" },
            Actor, CancellationToken.None);

        var created = await fixture.Context.Exhibition.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal("北京", created.Location);
        Assert.Equal("c1.jpg", created.CoverPath);
        Assert.Contains("新增了此展览", created.EditRecord);

        // Cover-preserve-on-empty: an update with no new cover keeps the existing one.
        await repo.UpdateAsync(
            new Exhibition { Id = id, ArtistId = 5, Title = "个展2", Location = "上海", CoverPath = null },
            Actor, CancellationToken.None);
        var updated = await fixture.Context.Exhibition.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal("个展2", updated.Title);
        Assert.Equal("上海", updated.Location);
        Assert.Equal("c1.jpg", updated.CoverPath);
        Assert.Contains("修改了此展览", updated.EditRecord);

        var page = await repo.GetPagedAsync(1, 10, CancellationToken.None);
        Assert.Equal("李四", page.Items.Single().ArtistName);

        Assert.True(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.Exhibition.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Fav_create_persists_distinctive_fields()
    {
        await using var fixture = CreateContext();
        var repo = new FavAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repo.CreateAsync(
            new Fav { ArtistId = 1, Title = "青花瓷", Material = "瓷", CollectNumber = "A-001" },
            Actor, CancellationToken.None);

        var saved = await fixture.Context.Fav.AsNoTracking().SingleAsync(f => f.Id == id);
        Assert.Equal("瓷", saved.Material);
        Assert.Equal("A-001", saved.CollectNumber);
    }

    [Fact]
    public async Task Auction_create_persists_prices()
    {
        await using var fixture = CreateContext();
        var repo = new AuctionAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repo.CreateAsync(
            new Auction { ArtistId = 1, Title = "山水", Price = "100万", RMB = "1000000" },
            Actor, CancellationToken.None);

        var saved = await fixture.Context.Auction.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal("100万", saved.Price);
        Assert.Equal("1000000", saved.RMB);
    }

    [Fact]
    public async Task Works_create_update_delete_and_preserves_image_on_empty()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 7, "王五");
        var repo = new WorksAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repo.CreateAsync(
            new Works { ArtistId = 7, Title = "山居图", Content = "布面油画，120 X 180cm", Tags = "山水", Path = "p1.jpg" },
            Actor, CancellationToken.None);

        var created = await fixture.Context.Works.AsNoTracking().SingleAsync(w => w.Id == id);
        Assert.Equal("p1.jpg", created.Path);
        Assert.Contains("新增了此作品", created.EditRecord);

        // Cover-preserve-on-empty: an update with no new image keeps the existing one.
        await repo.UpdateAsync(
            new Works { Id = id, ArtistId = 7, Title = "山居图2", Content = "纸本水墨", Tags = "山水,水墨", Path = null },
            Actor, CancellationToken.None);
        var updated = await fixture.Context.Works.AsNoTracking().SingleAsync(w => w.Id == id);
        Assert.Equal("山居图2", updated.Title);
        Assert.Equal("纸本水墨", updated.Content);
        Assert.Equal("p1.jpg", updated.Path);
        Assert.Contains("修改了此作品", updated.EditRecord);

        var page = await repo.GetPagedAsync(1, 10, CancellationToken.None);
        Assert.Equal("王五", page.Items.Single().ArtistName);

        Assert.True(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.Works.AsNoTracking().ToListAsync());
    }

    private static void SeedArtist(SzaipaAdminContext context, int id, string name)
    {
        context.Artist.Add(new Artist { Id = id, ArtistNameCN = name });
        context.SaveChanges();
    }

    private static AdminContextFixture CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options;
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
