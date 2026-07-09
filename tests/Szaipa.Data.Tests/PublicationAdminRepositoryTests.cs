using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side Publication (exhibition) repository against a SQLite in-memory database with the
/// same schema as the admin context. Covers create/update/delete, cover/logo preserve-on-empty, and the
/// gallery-sync-only <see cref="PublicationAdminRepository.SetMaxImgAsync"/> path.
/// </summary>
public sealed class PublicationAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_persists_editable_fields_and_server_owned_fields()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new Publication
            {
                TitleCN = "水墨新境",
                TitleEN = "New Ink",
                FolderName = "shuimo2026",
                Location = "深圳",
                Status = true,
                Type = 1,
                CoverPath = "cover.jpg",
                LogoPath = "logo.jpg"
            },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Publication.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal("水墨新境", saved.TitleCN);
        Assert.Equal("shuimo2026", saved.FolderName);
        Assert.Equal(0, saved.ReadCount);
        Assert.Contains("新建了此展览", saved.EditRecord);
    }

    [Fact]
    public async Task CreateAsync_writes_operation_record_to_diary_and_staff()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());

        await repository.CreateAsync(new Publication { TitleCN = "个展A" }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Equal(DateTime.Now.Date, diary.Date);
        Assert.Contains("新建了 个展A的展览", diary.OperationRecord);

        var staff = await fixture.Context.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("新建了 个展A的展览", staff.OperationRecord);
    }

    [Fact]
    public async Task UpdateAsync_changes_editable_fields_and_keeps_cover_and_logo_when_input_is_empty()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(
            new Publication { TitleCN = "原名", CoverPath = "old-cover.jpg", LogoPath = "old-logo.jpg" },
            Actor,
            CancellationToken.None);

        await repository.UpdateAsync(
            new Publication { Id = id, TitleCN = "新名", Location = "北京", CoverPath = null, LogoPath = null },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Publication.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal("新名", saved.TitleCN);
        Assert.Equal("北京", saved.Location);
        Assert.Equal("old-cover.jpg", saved.CoverPath);
        Assert.Equal("old-logo.jpg", saved.LogoPath);
        Assert.Contains("修改了此展览", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.UpdateAsync(
            new Publication { Id = 999, TitleCN = "x" }, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task SetMaxImgAsync_updates_only_maximg_without_touching_editrecord_or_operation_log()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Publication { TitleCN = "画廊展" }, Actor, CancellationToken.None);
        // CreateAsync itself writes one Diary row via IOperationRecorder; SetMaxImgAsync must not add a second.
        var diaryCountAfterCreate = await fixture.Context.Diary.AsNoTracking().CountAsync();

        Assert.True(await repository.SetMaxImgAsync(id, 7, CancellationToken.None));

        var saved = await fixture.Context.Publication.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal(7, saved.MaxImg);
        Assert.DoesNotContain("修改了此展览", saved.EditRecord);
        Assert.Equal(diaryCountAfterCreate, await fixture.Context.Diary.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task SetMaxImgAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.SetMaxImgAsync(999, 3, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_removes_row()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Publication { TitleCN = "待删" }, Actor, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.Publication.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetPagedAsync_orders_by_id_desc_and_pages()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        for (var i = 0; i < 5; i++)
        {
            await repository.CreateAsync(new Publication { TitleCN = $"p{i}" }, Actor, CancellationToken.None);
        }

        var page1 = await repository.GetPagedAsync(1, 2, CancellationToken.None);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(new[] { 5, 4 }, page1.Items.Select(p => p.Id).ToArray());

        var page3 = await repository.GetPagedAsync(3, 2, CancellationToken.None);
        Assert.Equal(new[] { 1 }, page3.Items.Select(p => p.Id).ToArray());
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
