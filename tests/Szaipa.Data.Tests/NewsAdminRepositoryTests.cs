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

    [Fact]
    public async Task SetImportantAsync_toggles_only_the_flag_and_records_it()
    {
        await using var fixture = CreateContext();
        var repository = new NewsAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new News { Title = "展讯", Content = "<p>x</p>", CoverPath = "a.jpg" }, Actor, CancellationToken.None);

        Assert.True(await repository.SetImportantAsync(id, true, Actor, CancellationToken.None));
        var saved = await fixture.Context.News.AsNoTracking().SingleAsync(n => n.Id == id);
        Assert.True(saved.Important);
        Assert.Equal("<p>x</p>", saved.Content);
        Assert.Contains("设为展示封面", saved.EditRecord);

        Assert.True(await repository.SetImportantAsync(id, false, Actor, CancellationToken.None));
        Assert.False((await fixture.Context.News.AsNoTracking().SingleAsync(n => n.Id == id)).Important);
        Assert.False(await repository.SetImportantAsync(999, true, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task MarkLeadArticlesImportantAsync_marks_the_first_visible_article_of_each_push_only()
    {
        await using var fixture = CreateContext();
        var db = fixture.Context;
        // 群发 A：头条 1、次条 2；群发 B：头条 3 被隐藏 → 次条 4 顶上；群发 C：头条 5 没封面 → 不标；
        // 单篇 6（url: 键）；7 已被手动标重要的次条保持不变；群发 D 的头条 8 已是重要。
        db.News.AddRange(
            new News { Id = 1, Title = "A1", CoverPath = "1.jpg", Important = false },
            new News { Id = 2, Title = "A2", CoverPath = "2.jpg", Important = false },
            new News { Id = 3, Title = "B1", CoverPath = "3.jpg", Important = false },
            new News { Id = 4, Title = "B2", CoverPath = "4.jpg", Important = false },
            new News { Id = 5, Title = "C1", CoverPath = null, Important = false },
            new News { Id = 6, Title = "S", CoverPath = "6.jpg", Important = null },
            new News { Id = 7, Title = "A3", CoverPath = "7.jpg", Important = true },
            new News { Id = 8, Title = "D1", CoverPath = "8.jpg", Important = true });
        db.WeChatSyncRecord.AddRange(
            Record(1, "mp:BIZ==:100:1", 0),
            Record(2, "mp:BIZ==:100:2", 1),
            Record(7, "mp:BIZ==:100:3", 2),
            Record(3, "mp:BIZ==:200:1", 0, hidden: true),
            Record(4, "mp:BIZ==:200:2", 1),
            Record(5, "mp:BIZ==:300:1", 0),
            Record(6, "url:https://mp.weixin.qq.com/s/abc", 0),
            Record(8, "mp:BIZ==:400:1", 0));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new Szaipa.Data.Services.WeChat.WeChatSyncAdminRepository(db, new OperationRecorder());

        Assert.Equal(3, await repository.MarkLeadArticlesImportantAsync(Actor, CancellationToken.None));

        var important = await db.News.AsNoTracking().Where(n => n.Important == true).Select(n => n.Id).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { 1, 4, 6, 7, 8 }, important);
        Assert.Equal(0, await repository.MarkLeadArticlesImportantAsync(Actor, CancellationToken.None)); // 幂等

        static WeChatSyncRecord Record(int newsId, string key, int index, bool hidden = false) => new()
        {
            SourceKey = key, ItemIndex = index, NewsId = newsId, IsHidden = hidden,
            ContentHash = "h", SyncedAt = DateTime.Now, Source = "link"
        };
    }

    [Theory]
    [InlineData("mp:BIZ==:100:2", "mp:BIZ==:100")]
    [InlineData("api:aid-1:0", "api:aid-1")]
    [InlineData("url:https://mp.weixin.qq.com/s/abc", "url:https://mp.weixin.qq.com/s/abc")]
    public void PushKey_groups_articles_of_the_same_push(string sourceKey, string expected) =>
        Assert.Equal(expected, Szaipa.Data.Services.WeChat.WeChatSyncAdminRepository.PushKey(sourceKey));

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
