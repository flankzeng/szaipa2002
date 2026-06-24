using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side 同构(Tongou) repositories. Tongou is a physically separate database from Szaipa,
/// so each test wires up two independent SQLite in-memory contexts (TongouAdminContext for the data,
/// SzaipaAdminContext for the Diary/Staff audit trail written by IOperationRecorder) — mirroring how the two
/// repositories are constructed in production against two real, separate SQL Server connections.
/// </summary>
public sealed class TongouAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Atrist_create_rejects_duplicate_name_and_persists_fields()
    {
        await using var fixture = CreateFixture();
        var repo = new TongouAtristAdminRepository(fixture.TongouContext, fixture.SzaipaContext, new OperationRecorder());

        var id = await repo.CreateAsync(
            new TongouAtrist { Name = "李雷", Title = "驻地艺术家", AboutText = "简介", HeardPath = "/Content/Tongou/Atrist/p1.jpg" },
            Actor, CancellationToken.None);

        var saved = await fixture.TongouContext.TongouAtrist.AsNoTracking().SingleAsync(a => a.id == id);
        Assert.Equal("李雷", saved.Name);
        Assert.Equal("/Content/Tongou/Atrist/p1.jpg", saved.HeardPath);

        Assert.True(await repo.NameExistsAsync("李雷", CancellationToken.None));
        Assert.False(await repo.NameExistsAsync("韩梅梅", CancellationToken.None));
    }

    [Fact]
    public async Task Atrist_create_writes_operation_record_to_szaipa_diary_and_staff()
    {
        await using var fixture = CreateFixture();
        fixture.SzaipaContext.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.SzaipaContext.SaveChangesAsync();
        var repo = new TongouAtristAdminRepository(fixture.TongouContext, fixture.SzaipaContext, new OperationRecorder());

        await repo.CreateAsync(new TongouAtrist { Name = "李雷" }, Actor, CancellationToken.None);

        var diary = await fixture.SzaipaContext.Diary.AsNoTracking().SingleAsync();
        Assert.Contains("创建了同构艺术家 李雷的条目", diary.OperationRecord);

        var staff = await fixture.SzaipaContext.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("创建了同构艺术家 李雷的条目", staff.OperationRecord);
    }

    [Fact]
    public async Task Atrist_update_changes_only_editable_fields_and_delete_removes_row()
    {
        await using var fixture = CreateFixture();
        var repo = new TongouAtristAdminRepository(fixture.TongouContext, fixture.SzaipaContext, new OperationRecorder());
        var id = await repo.CreateAsync(new TongouAtrist { Name = "原名" }, Actor, CancellationToken.None);

        await repo.UpdateAsync(new TongouAtrist { id = id, Name = "新名", Title = "新头衔" }, Actor, CancellationToken.None);
        var saved = await fixture.TongouContext.TongouAtrist.AsNoTracking().SingleAsync(a => a.id == id);
        Assert.Equal("新名", saved.Name);
        Assert.Equal("新头衔", saved.Title);

        Assert.True(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.TongouContext.TongouAtrist.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Works_create_resolves_artist_name_and_list_joins_artist()
    {
        await using var fixture = CreateFixture();
        fixture.TongouContext.TongouAtrist.Add(new TongouAtrist { id = 9, Name = "王五" });
        await fixture.TongouContext.SaveChangesAsync();
        var repo = new TongouWorksAdminRepository(fixture.TongouContext, fixture.SzaipaContext, new OperationRecorder());

        var id = await repo.CreateAsync(
            new TongouWorks { Atristid = 9, Title = "山水", Size = "120x80", Type = "油画", ImgPath = "/Content/Tongou/Works/w1.jpg" },
            Actor, CancellationToken.None);

        var saved = await fixture.TongouContext.TongouWorks.AsNoTracking().SingleAsync(w => w.id == id);
        Assert.Equal("王五", saved.AtristidName);
        Assert.Equal(0, saved.VisityCount);

        var page = await repo.GetPagedAsync(1, 10, CancellationToken.None);
        Assert.Equal("王五", page.Items.Single().ArtistName);
    }

    [Fact]
    public async Task Works_update_preserves_artist_and_image_on_empty_then_delete()
    {
        await using var fixture = CreateFixture();
        fixture.TongouContext.TongouAtrist.Add(new TongouAtrist { id = 3, Name = "赵六" });
        await fixture.TongouContext.SaveChangesAsync();
        var repo = new TongouWorksAdminRepository(fixture.TongouContext, fixture.SzaipaContext, new OperationRecorder());
        var id = await repo.CreateAsync(
            new TongouWorks { Atristid = 3, Title = "原题", ImgPath = "/Content/Tongou/Works/old.jpg" },
            Actor, CancellationToken.None);

        await repo.UpdateAsync(
            new TongouWorks { id = id, Atristid = 999, Title = "新题", ImgPath = null },
            Actor, CancellationToken.None);

        var saved = await fixture.TongouContext.TongouWorks.AsNoTracking().SingleAsync(w => w.id == id);
        Assert.Equal("新题", saved.Title);
        Assert.Equal(3, saved.Atristid); // artist reassignment is not supported by update, matching legacy WorkEdit
        Assert.Equal("/Content/Tongou/Works/old.jpg", saved.ImgPath);

        Assert.True(await repo.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.TongouContext.TongouWorks.AsNoTracking().ToListAsync());
    }

    private static TongouFixture CreateFixture()
    {
        var tongouConnection = new SqliteConnection("DataSource=:memory:");
        tongouConnection.Open();
        var tongouOptions = new DbContextOptionsBuilder<TongouAdminContext>().UseSqlite(tongouConnection).Options;
        var tongouContext = new TongouAdminContext(tongouOptions);
        tongouContext.Database.EnsureCreated();

        var szaipaConnection = new SqliteConnection("DataSource=:memory:");
        szaipaConnection.Open();
        var szaipaOptions = new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(szaipaConnection).Options;
        var szaipaContext = new SzaipaAdminContext(szaipaOptions);
        szaipaContext.Database.EnsureCreated();

        return new TongouFixture(tongouContext, tongouConnection, szaipaContext, szaipaConnection);
    }

    private sealed class TongouFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _tongouConnection;
        private readonly SqliteConnection _szaipaConnection;

        public TongouFixture(
            TongouAdminContext tongouContext,
            SqliteConnection tongouConnection,
            SzaipaAdminContext szaipaContext,
            SqliteConnection szaipaConnection)
        {
            TongouContext = tongouContext;
            _tongouConnection = tongouConnection;
            SzaipaContext = szaipaContext;
            _szaipaConnection = szaipaConnection;
        }

        public TongouAdminContext TongouContext { get; }

        public SzaipaAdminContext SzaipaContext { get; }

        public async ValueTask DisposeAsync()
        {
            await TongouContext.DisposeAsync();
            _tongouConnection.Dispose();
            await SzaipaContext.DisposeAsync();
            _szaipaConnection.Dispose();
        }
    }
}
