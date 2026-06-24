using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side Artist repository against a SQLite in-memory database with the same schema as
/// the admin context. Pins behaviours that mirror the legacy controller's intent but are now correct: the
/// avatar/banner images are preserved on update when left blank, server-owned counters/dates are set once,
/// and EndDate is refreshed on every edit (as legacy ArtEdit did).
/// </summary>
public sealed class ArtistAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_persists_editable_fields_and_server_owned_fields()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new Artist
            {
                ArtistNameCN = "张三",
                ArtistNameEN = "Zhang San",
                Sex = "男",
                Nation = "中国",
                City = "深圳",
                Title = "会员｜艺术家",
                Position = "理事",
                Color1 = "#3e47e1",
                Color2 = "#151965",
                Introduction = "简介内容",
                Honor = "荣誉内容",
                DeedsThings = "<p>2020 年表</p>",
                Path = "avatar.jpg",
                Path1 = "banner1.jpg",
                Path2 = "banner2.jpg"
            },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Artist.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal("张三", saved.ArtistNameCN);
        Assert.Equal("avatar.jpg", saved.Path);
        Assert.Equal("banner1.jpg", saved.Path1);
        Assert.Equal("banner2.jpg", saved.Path2);
        Assert.Equal("<p>2020 年表</p>", saved.DeedsThings);
        Assert.Equal(0, saved.WorkCount);
        Assert.Equal(0, saved.VisitCount);
        Assert.NotNull(saved.AddDate);
        Assert.NotNull(saved.EndDate);
        Assert.Contains("创建了此会员的条目", saved.EditRecord);
    }

    [Fact]
    public async Task CreateAsync_writes_operation_record_to_diary_and_staff()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());

        await repository.CreateAsync(new Artist { ArtistNameCN = "李四" }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Equal(DateTime.Now.Date, diary.Date);
        Assert.Contains("创建了 李四的会员条目", diary.OperationRecord);

        var staff = await fixture.Context.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("创建了 李四的会员条目", staff.OperationRecord);
    }

    [Fact]
    public async Task UpdateAsync_changes_editable_fields_and_appends_edit_record()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Artist { ArtistNameCN = "原名" }, Actor, CancellationToken.None);

        var updated = await repository.UpdateAsync(
            new Artist { Id = id, ArtistNameCN = "新名", City = "上海" },
            Actor,
            CancellationToken.None);

        Assert.True(updated);
        var saved = await fixture.Context.Artist.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal("新名", saved.ArtistNameCN);
        Assert.Equal("上海", saved.City);
        Assert.Contains("修改了此会员的条目", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_keeps_existing_images_when_input_paths_are_empty()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(
            new Artist { ArtistNameCN = "原名", Path = "old.jpg", Path1 = "old1.jpg", Path2 = "old2.jpg" },
            Actor,
            CancellationToken.None);

        await repository.UpdateAsync(
            new Artist { Id = id, ArtistNameCN = "原名", Path = null, Path1 = null, Path2 = null },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Artist.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.Equal("old.jpg", saved.Path);
        Assert.Equal("old1.jpg", saved.Path1);
        Assert.Equal("old2.jpg", saved.Path2);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.UpdateAsync(new Artist { Id = 999, ArtistNameCN = "x" }, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_removes_row()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Artist { ArtistNameCN = "待删" }, Actor, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.Artist.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetPagedAsync_orders_by_id_desc_and_pages()
    {
        await using var fixture = CreateContext();
        var repository = new ArtistAdminRepository(fixture.Context, new OperationRecorder());
        for (var i = 0; i < 5; i++)
        {
            await repository.CreateAsync(new Artist { ArtistNameCN = $"a{i}" }, Actor, CancellationToken.None);
        }

        var page1 = await repository.GetPagedAsync(1, 2, CancellationToken.None);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(new[] { 5, 4 }, page1.Items.Select(a => a.Id).ToArray());

        var page3 = await repository.GetPagedAsync(3, 2, CancellationToken.None);
        Assert.Equal(new[] { 1 }, page3.Items.Select(a => a.Id).ToArray());
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
