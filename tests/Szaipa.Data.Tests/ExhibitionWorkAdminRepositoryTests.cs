using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the write-side works-catalog (参展作品目录) repository against a SQLite in-memory database:
/// replace-on-save reorders/adds/drops rows with a fresh contiguous SortOrder, scoping is per-publication,
/// and the operation record is written.
/// </summary>
public sealed class ExhibitionWorkAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task ReplaceAsync_inserts_rows_in_order_with_contiguous_sort_order()
    {
        await using var fixture = CreateContext();
        var repository = new ExhibitionWorkAdminRepository(fixture.Context, new OperationRecorder());

        await repository.ReplaceAsync(
            1,
            new[]
            {
                new ExhibitionWorkInput("绘画", "作品一", "艺术家甲", "100x80cm", "布面油画", "/Content/images/a/w1.jpg"),
                new ExhibitionWorkInput("雕塑", "作品二", "艺术家乙", "50cm", "青铜", "/Content/images/a/w2.jpg")
            },
            Actor,
            CancellationToken.None);

        var saved = await repository.GetByPublicationAsync(1, CancellationToken.None);
        Assert.Equal(new[] { "作品一", "作品二" }, saved.Select(w => w.Title).ToArray());
        Assert.Equal(new[] { 0, 1 }, saved.Select(w => w.SortOrder).ToArray());
        Assert.Equal("绘画", saved[0].Category);
        Assert.Equal("艺术家乙", saved[1].Artist);
    }

    [Fact]
    public async Task ReplaceAsync_reorders_and_drops_rows_on_a_second_save()
    {
        await using var fixture = CreateContext();
        var repository = new ExhibitionWorkAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(
            1,
            new[]
            {
                new ExhibitionWorkInput(null, "一", null, null, null, null),
                new ExhibitionWorkInput(null, "二", null, null, null, null),
                new ExhibitionWorkInput(null, "三", null, null, null, null)
            },
            Actor,
            CancellationToken.None);

        // Drop "二", swap order of the remaining two.
        await repository.ReplaceAsync(
            1,
            new[]
            {
                new ExhibitionWorkInput(null, "三", null, null, null, null),
                new ExhibitionWorkInput(null, "一", null, null, null, null)
            },
            Actor,
            CancellationToken.None);

        var saved = await repository.GetByPublicationAsync(1, CancellationToken.None);
        Assert.Equal(new[] { "三", "一" }, saved.Select(w => w.Title).ToArray());
        Assert.Equal(new[] { 0, 1 }, saved.Select(w => w.SortOrder).ToArray());
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_list_clears_the_catalog()
    {
        await using var fixture = CreateContext();
        var repository = new ExhibitionWorkAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(1, new[] { new ExhibitionWorkInput(null, "一", null, null, null, null) }, Actor, CancellationToken.None);

        await repository.ReplaceAsync(1, Array.Empty<ExhibitionWorkInput>(), Actor, CancellationToken.None);

        Assert.Empty(await repository.GetByPublicationAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task ReplaceAsync_scopes_rows_to_the_given_publication()
    {
        await using var fixture = CreateContext();
        var repository = new ExhibitionWorkAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(1, new[] { new ExhibitionWorkInput(null, "属于展览1", null, null, null, null) }, Actor, CancellationToken.None);
        await repository.ReplaceAsync(2, new[] { new ExhibitionWorkInput(null, "属于展览2", null, null, null, null) }, Actor, CancellationToken.None);

        Assert.Equal("属于展览1", Assert.Single(await repository.GetByPublicationAsync(1, CancellationToken.None)).Title);
        Assert.Equal("属于展览2", Assert.Single(await repository.GetByPublicationAsync(2, CancellationToken.None)).Title);
    }

    [Fact]
    public async Task ReplaceAsync_writes_operation_record()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Contexts.Szaipa.Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new ExhibitionWorkAdminRepository(fixture.Context, new OperationRecorder());

        await repository.ReplaceAsync(1, new[] { new ExhibitionWorkInput(null, "一", null, null, null, null) }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Contains("参展作品目录", diary.OperationRecord);
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
