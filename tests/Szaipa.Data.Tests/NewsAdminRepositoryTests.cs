using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side News repository against a SQLite in-memory database with the same schema as the
/// admin context. These tests pin the behaviours that were broken in the legacy controller: the editor body
/// is persisted, fields are actually updated, and operation records are written — all in one transaction.
/// </summary>
public sealed class NewsAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_persists_content_and_server_owned_fields()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new News { Title = "标题", Subtitle = "简介", Content = "<p>正文</p>", Important = true },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.News.AsNoTracking().SingleAsync(n => n.Id == id);
        Assert.Equal("<p>正文</p>", saved.Content); // legacy NewsAdd dropped this
        Assert.Equal("标题", saved.Title);
        Assert.Equal("tester", saved.Autor);
        Assert.Equal(0, saved.ReadCount);
        Assert.True(saved.Important);
        Assert.NotNull(saved.Date);
        Assert.Contains("编写了此新闻条目", saved.EditRecord);
    }

    [Fact]
    public async Task CreateAsync_writes_operation_record_to_diary_and_staff()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());

        await repository.CreateAsync(new News { Title = "选举" }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Equal(DateTime.Now.Date, diary.Date);
        Assert.Contains("编写了 选举的新闻条目", diary.OperationRecord);

        var staff = await fixture.Context.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("编写了 选举的新闻条目", staff.OperationRecord);
    }

    [Fact]
    public async Task UpdateAsync_changes_editable_fields_and_appends_edit_record()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new News { Title = "原标题", Content = "<p>old</p>" }, Actor, CancellationToken.None);

        var updated = await repository.UpdateAsync(
            new News { Id = id, Title = "新标题", Content = "<p>new</p>" },
            Actor,
            CancellationToken.None);

        Assert.True(updated);
        var saved = await fixture.Context.News.AsNoTracking().SingleAsync(n => n.Id == id);
        Assert.Equal("新标题", saved.Title);
        Assert.Equal("<p>new</p>", saved.Content);
        Assert.Contains("修改了此新闻条目", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_keeps_existing_cover_when_input_cover_is_empty()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new News { Title = "t", CoverPath = "abc.jpg" }, Actor, CancellationToken.None);

        await repository.UpdateAsync(new News { Id = id, Title = "t2", CoverPath = null }, Actor, CancellationToken.None);

        var saved = await fixture.Context.News.AsNoTracking().SingleAsync(n => n.Id == id);
        Assert.Equal("abc.jpg", saved.CoverPath);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.UpdateAsync(new News { Id = 999, Title = "x" }, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_removes_row()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new News { Title = "待删" }, Actor, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.News.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetPagedAsync_orders_by_id_desc_and_pages()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());
        for (var i = 0; i < 5; i++)
        {
            await repository.CreateAsync(new News { Title = $"n{i}" }, Actor, CancellationToken.None);
        }

        var page1 = await repository.GetPagedAsync(1, 2, CancellationToken.None);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(new[] { 5, 4 }, page1.Items.Select(n => n.Id).ToArray());

        var page3 = await repository.GetPagedAsync(3, 2, CancellationToken.None);
        Assert.Equal(new[] { 1 }, page3.Items.Select(n => n.Id).ToArray());
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
