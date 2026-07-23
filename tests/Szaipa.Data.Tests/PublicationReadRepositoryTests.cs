using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class PublicationReadRepositoryTests
{
    [Fact]
    public async Task GetLatestPublicationsAsync_orders_by_id_desc_and_caps()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        var result = await repository.GetLatestPublicationsAsync(2);

        Assert.Equal(new[] { 3, 2 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetLatestPublicationsAsync_zero_returns_all_id_desc()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        var result = await repository.GetLatestPublicationsAsync(0);

        Assert.Equal(new[] { 3, 2, 1 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetPublicationByIdAsync_maps_card_fields_and_nullable_status()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        var p1 = await repository.GetPublicationByIdAsync(1);
        var p3 = await repository.GetPublicationByIdAsync(3);

        Assert.NotNull(p1);
        Assert.Equal("f1", p1!.FolderName);
        Assert.Equal("org1", p1.Organizer);
        Assert.Equal("host1", p1.Host);
        Assert.Equal("co1", p1.CoHost);
        Assert.True(p1.Status);
        Assert.Null(p3!.Status); // null Status preserved
    }

    [Fact]
    public async Task GetPublicationByIdAsync_returns_null_when_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        Assert.Null(await repository.GetPublicationByIdAsync(999));
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_returns_detail_and_capped_related()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(2, relatedCount: 2);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.Publication.Id);
        Assert.Equal("org2", snapshot.Publication.Organizer); // zhuban -> Organizer
        Assert.Equal("host2", snapshot.Publication.Host);     // chengban -> Host
        Assert.Equal("co2", snapshot.Publication.CoHost);     // xieban -> CoHost
        Assert.Equal("logo2", snapshot.Publication.LogoPath);
        Assert.Equal(new[] { 3, 2 }, snapshot.RelatedPublications.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_returns_works_ordered_by_sort_order()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        fixture.Context.Database.ExecuteSqlRaw("UPDATE Publication SET Type = 1 WHERE Id = 2");
        InsertWork(fixture.Context, id: 1, publicationId: 2, sortOrder: 1, title: "作品B");
        InsertWork(fixture.Context, id: 2, publicationId: 2, sortOrder: 0, title: "作品A");
        InsertWork(fixture.Context, id: 3, publicationId: 3, sortOrder: 0, title: "属于其它展览");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(2, relatedCount: 0);

        Assert.NotNull(snapshot);
        Assert.Equal(new[] { "作品A", "作品B" }, snapshot!.Publication.Works.Select(w => w.Title).ToArray());
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_returns_empty_works_when_none_configured()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(1, relatedCount: 0);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot!.Publication.Works);
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_keeps_legacy_gallery_available_when_optional_works_table_is_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        fixture.Context.Database.ExecuteSqlRaw("UPDATE Publication SET Type = 1 WHERE Id = 1");
        fixture.Context.Database.ExecuteSqlRaw("DROP TABLE ExhibitionWork");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(1, relatedCount: 0);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.Publication.Id);
        Assert.Empty(snapshot.Publication.Works);
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_keeps_legacy_gallery_available_when_template_columns_are_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN Type");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(1, relatedCount: 0);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.Publication.Id);
        Assert.Equal(0, snapshot.Publication.Type);
        Assert.Empty(snapshot.Publication.Works);
    }

    [Fact]
    public async Task GetPublicationDetailSnapshotAsync_returns_null_when_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new PublicationReadRepository(fixture.Context);

        Assert.Null(await repository.GetPublicationDetailSnapshotAsync(999, 5));
    }

    private static void Seed(SzaipaLegacyReadContext context)
    {
        Insert(context, 1, "P1", true, "f1", "org1", "host1", "co1", "logo1");
        Insert(context, 2, "P2", false, "f2", "org2", "host2", "co2", "logo2");
        Insert(context, 3, "P3", null, "f3", "org3", "host3", "co3", "logo3");
    }

    private static void Insert(
        SzaipaLegacyReadContext context,
        int id,
        string titleCn,
        bool? status,
        string folderName,
        string zhuban,
        string chengban,
        string xieban,
        string logoPath)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Publication (Id, TitleCN, MaxImg, ReadCount, Status, FolderName, zhuban, chengban, xieban, LogoPath) " +
            "VALUES (@id, @titleCn, 0, 0, @status, @folderName, @zhuban, @chengban, @xieban, @logoPath)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@titleCn", titleCn),
            new SqliteParameter("@status", (object?)status ?? DBNull.Value),
            new SqliteParameter("@folderName", folderName),
            new SqliteParameter("@zhuban", zhuban),
            new SqliteParameter("@chengban", chengban),
            new SqliteParameter("@xieban", xieban),
            new SqliteParameter("@logoPath", logoPath));
    }

    private static void InsertWork(
        SzaipaLegacyReadContext context,
        int id,
        int publicationId,
        int sortOrder,
        string title)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO ExhibitionWork (Id, PublicationId, Title, SortOrder) VALUES (@id, @publicationId, @title, @sortOrder)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@publicationId", publicationId),
            new SqliteParameter("@title", title),
            new SqliteParameter("@sortOrder", sortOrder));
    }
}
