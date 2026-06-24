using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side Company repository against a SQLite in-memory database with the same schema as
/// the admin context. Company is a standalone main table (no ArtistId), built independently like News.
/// </summary>
public sealed class CompanyAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task CreateAsync_persists_editable_fields_and_server_owned_fields()
    {
        await using var fixture = CreateContext();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new Company
            {
                NameCN = "赛丽美术馆",
                NameEN = "Szaipa Gallery",
                CEO = "张总",
                Business = "美术展览",
                Address = "深圳市福田区",
                ImgPath = "logo.jpg"
            },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Company.AsNoTracking().SingleAsync(c => c.Id == id);
        Assert.Equal("赛丽美术馆", saved.NameCN);
        Assert.Equal("logo.jpg", saved.ImgPath);
        Assert.Equal(0, saved.VisitCount);
        Assert.NotNull(saved.FirstDate);
        Assert.NotNull(saved.LastDate);
        Assert.Contains("编写了此会员企业条目", saved.EditRecord);
    }

    [Fact]
    public async Task CreateAsync_writes_operation_record_to_diary_and_staff()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());

        await repository.CreateAsync(new Company { NameCN = "赛丽" }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Equal(DateTime.Now.Date, diary.Date);
        Assert.Contains("编写了 赛丽会员企业条目", diary.OperationRecord);

        var staff = await fixture.Context.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("编写了 赛丽会员企业条目", staff.OperationRecord);
    }

    [Fact]
    public async Task UpdateAsync_changes_editable_fields_and_keeps_logo_when_input_is_empty()
    {
        await using var fixture = CreateContext();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(
            new Company { NameCN = "原名", ImgPath = "old.jpg" }, Actor, CancellationToken.None);

        await repository.UpdateAsync(
            new Company { Id = id, NameCN = "新名", CEO = "新法人", ImgPath = null },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Company.AsNoTracking().SingleAsync(c => c.Id == id);
        Assert.Equal("新名", saved.NameCN);
        Assert.Equal("新法人", saved.CEO);
        Assert.Equal("old.jpg", saved.ImgPath);
        Assert.Contains("修改了此会员企业条目", saved.EditRecord);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_missing()
    {
        await using var fixture = CreateContext();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());

        Assert.False(await repository.UpdateAsync(new Company { Id = 999, NameCN = "x" }, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_removes_row()
    {
        await using var fixture = CreateContext();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Company { NameCN = "待删" }, Actor, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(id, Actor, CancellationToken.None));
        Assert.Empty(await fixture.Context.Company.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetPagedAsync_orders_by_id_desc_and_pages()
    {
        await using var fixture = CreateContext();
        var repository = new CompanyAdminRepository(fixture.Context, new OperationRecorder());
        for (var i = 0; i < 5; i++)
        {
            await repository.CreateAsync(new Company { NameCN = $"c{i}" }, Actor, CancellationToken.None);
        }

        var page1 = await repository.GetPagedAsync(1, 2, CancellationToken.None);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(new[] { 5, 4 }, page1.Items.Select(c => c.Id).ToArray());

        var page3 = await repository.GetPagedAsync(3, 2, CancellationToken.None);
        Assert.Equal(new[] { 1 }, page3.Items.Select(c => c.Id).ToArray());
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
