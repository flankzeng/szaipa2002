using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Write-side ArtNews repository over a SQLite in-memory admin schema. Pins that the editor body and the
/// artist link are persisted, that edits actually change fields (the legacy ArtNewsEdit was a no-op), and
/// that the list joins the artist's Chinese name.
/// </summary>
public sealed class ArtNewsAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_persists_content_artist_and_date()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 7, "张三");
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new ArtNews { ArtistId = 7, Title = "个展", Content = "<p>正文</p>", Date = new DateTime(2026, 5, 1) },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.ArtNews.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal(7, saved.ArtistId);
        Assert.Equal("<p>正文</p>", saved.Content);
        Assert.Equal(new DateTime(2026, 5, 1), saved.Date);
        Assert.Equal(0, saved.ReadCount);
        Assert.Contains("编写了此艺术家动态", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_actually_changes_fields()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 1, "甲");
        SeedArtist(fixture.Context, 2, "乙");
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(
            new ArtNews { ArtistId = 1, Title = "旧", Content = "<p>old</p>" }, Actor, CancellationToken.None);

        var ok = await repository.UpdateAsync(
            new ArtNews { Id = id, ArtistId = 2, Title = "新", Content = "<p>new</p>" }, Actor, CancellationToken.None);

        Assert.True(ok);
        var saved = await fixture.Context.ArtNews.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal(2, saved.ArtistId);
        Assert.Equal("新", saved.Title);
        Assert.Equal("<p>new</p>", saved.Content);
        Assert.Contains("修改了此艺术家动态", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.UpdateAsync(
            new ArtNews { Id = 999, Title = "x" }, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_removes_row_and_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 1, "甲");
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(
            new ArtNews { ArtistId = 1, Title = "待删" }, Actor, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.ArtNews.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetPagedAsync_joins_artist_name_and_orders_desc()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 3, "王五");
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());
        await repository.CreateAsync(new ArtNews { ArtistId = 3, Title = "A" }, Actor, CancellationToken.None);
        await repository.CreateAsync(new ArtNews { ArtistId = 3, Title = "B" }, Actor, CancellationToken.None);

        var page = await repository.GetPagedAsync(1, 10, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "B", "A" }, page.Items.Select(i => i.Title).ToArray());
        Assert.All(page.Items, i => Assert.Equal("王五", i.ArtistName));
    }

    [Fact]
    public async Task GetArtistOptionsAsync_lists_artists()
    {
        await using var fixture = CreateContext();
        SeedArtist(fixture.Context, 1, "甲");
        SeedArtist(fixture.Context, 2, "乙");
        var repository = new ArtNewsAdminRepository(fixture.Context, new OperationRecorder());

        var options = await repository.GetArtistOptionsAsync(CancellationToken.None);

        Assert.Equal(2, options.Count);
        Assert.Contains(options, o => o is { Id: 1, Name: "甲" });
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
