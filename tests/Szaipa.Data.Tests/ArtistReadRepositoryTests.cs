using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class ArtistReadRepositoryTests
{
    [Fact]
    public async Task GetArtistsAsync_orders_by_id_desc_then_asc()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new ArtistReadRepository(fixture.Context);

        var newest = await repository.GetArtistsAsync(newestFirst: true);
        var oldest = await repository.GetArtistsAsync(newestFirst: false);

        Assert.Equal(new[] { 2, 1 }, newest.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, oldest.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetArtistByIdAsync_maps_fields_and_returns_null_when_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new ArtistReadRepository(fixture.Context);

        var artist = await repository.GetArtistByIdAsync(1);

        Assert.NotNull(artist);
        Assert.Equal("CN1", artist!.ArtistNameCn);
        Assert.Equal("EN1", artist.ArtistNameEn);
        Assert.Null(await repository.GetArtistByIdAsync(999));
    }

    [Fact]
    public async Task GetArtistWorksAsync_filters_by_artist()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new ArtistReadRepository(fixture.Context);

        var works = await repository.GetArtistWorksAsync(1);

        Assert.Equal(new[] { 100, 101 }, works.Select(item => item.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetArtistProfileAsync_composes_all_feeds_with_legacy_semantics()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new ArtistReadRepository(fixture.Context);

        var profile = await repository.GetArtistProfileAsync(1);

        Assert.NotNull(profile);
        Assert.Equal(1, profile!.Artist.Id);
        Assert.Equal(2, profile.Works.Count);

        // Side panels: unordered Take(1) => exactly one, the first (lowest-id) ArtNews row (10), not 11.
        Assert.Single(profile.LatestNews);
        Assert.Equal(10, profile.LatestNews[0].Id);

        // The "publication" panel reuses ArtNews (legacy placeholder).
        Assert.Single(profile.LatestPublications);
        Assert.Equal(10, profile.LatestPublications[0].Id);

        // Fav subtitle <- Location; Auction subtitle <- Price.
        Assert.Single(profile.LatestFavorites);
        Assert.Equal("Loc20", profile.LatestFavorites[0].Subtitle);
        Assert.Single(profile.LatestAuctions);
        Assert.Equal("Price30", profile.LatestAuctions[0].Subtitle);

        // Per-artist exhibition feed (string dates).
        Assert.Single(profile.RelatedExhibitions);
        Assert.Equal(40, profile.RelatedExhibitions[0].Id);
        Assert.Equal("2024", profile.RelatedExhibitions[0].StartDate);
    }

    [Fact]
    public async Task GetArtistProfileAsync_returns_null_when_artist_missing()
    {
        await using var fixture = TestDb.Szaipa();
        Seed(fixture.Context);
        var repository = new ArtistReadRepository(fixture.Context);

        Assert.Null(await repository.GetArtistProfileAsync(999));
    }

    private static void Seed(SzaipaLegacyReadContext context)
    {
        InsertArtist(context, 1, "CN1", "EN1");
        InsertArtist(context, 2, "CN2", "EN2");

        InsertWork(context, 100, artistId: 1);
        InsertWork(context, 101, artistId: 1);
        InsertWork(context, 200, artistId: 2);

        // Two ArtNews for artist 1 to exercise the unordered Take(1).
        InsertArtNews(context, 10, artistId: 1, title: "AN10", subTitle: "sub10");
        InsertArtNews(context, 11, artistId: 1, title: "AN11", subTitle: "sub11");

        InsertFav(context, 20, artistId: 1, title: "Fav20", location: "Loc20");
        InsertAuction(context, 30, artistId: 1, title: "Auc30", price: "Price30");
        InsertExhibition(context, 40, artistId: 1, title: "Exh40", startDate: "2024", endDate: "2025");
    }

    private static void InsertArtist(SzaipaLegacyReadContext context, int id, string cn, string en)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Artist (Id, ArtistNameCN, ArtistNameEN, Title, Path, Introduction, Nation, City, Honor, Path1, Path2) " +
            "VALUES (@id, @cn, @en, 'T', 'p', 'intro', 'N', 'C', 'H', 'p1', 'p2')",
            new SqliteParameter("@id", id),
            new SqliteParameter("@cn", cn),
            new SqliteParameter("@en", en));
    }

    private static void InsertWork(SzaipaLegacyReadContext context, int id, int artistId)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Works (Id, ArtistId, Width, Height, transverse, \"long\", Title, Path, Tags) " +
            "VALUES (@id, @artistId, 0, 0, 0, 0, 'W', 'wp', 'tag')",
            new SqliteParameter("@id", id),
            new SqliteParameter("@artistId", artistId));
    }

    private static void InsertArtNews(SzaipaLegacyReadContext context, int id, int artistId, string title, string subTitle)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO ArtNews (Id, ArtistId, Title, SubTitle, CoverPath) VALUES (@id, @artistId, @title, @subTitle, 'c.jpg')",
            new SqliteParameter("@id", id),
            new SqliteParameter("@artistId", artistId),
            new SqliteParameter("@title", title),
            new SqliteParameter("@subTitle", subTitle));
    }

    private static void InsertFav(SzaipaLegacyReadContext context, int id, int artistId, string title, string location)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Fav (Id, ArtistId, Title, Location, CoverPath) VALUES (@id, @artistId, @title, @location, 'c.jpg')",
            new SqliteParameter("@id", id),
            new SqliteParameter("@artistId", artistId),
            new SqliteParameter("@title", title),
            new SqliteParameter("@location", location));
    }

    private static void InsertAuction(SzaipaLegacyReadContext context, int id, int artistId, string title, string price)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Auction (Id, ArtistId, Title, Price, CoverPath) VALUES (@id, @artistId, @title, @price, 'c.jpg')",
            new SqliteParameter("@id", id),
            new SqliteParameter("@artistId", artistId),
            new SqliteParameter("@title", title),
            new SqliteParameter("@price", price));
    }

    private static void InsertExhibition(
        SzaipaLegacyReadContext context, int id, int artistId, string title, string startDate, string endDate)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Exhibition (Id, ArtistId, Title, CoverPath, Location, Link, StartDate, EndDate) " +
            "VALUES (@id, @artistId, @title, 'c.jpg', 'loc', 'lnk', @startDate, @endDate)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@artistId", artistId),
            new SqliteParameter("@title", title),
            new SqliteParameter("@startDate", startDate),
            new SqliteParameter("@endDate", endDate));
    }
}
