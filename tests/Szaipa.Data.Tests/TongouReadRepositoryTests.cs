using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Services.Tongou;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class TongouReadRepositoryTests
{
    [Fact]
    public async Task GetArtistByIdAsync_maps_lowercase_id_and_returns_null_when_missing()
    {
        await using var fixture = TestDb.Tongou();
        Seed(fixture.Context);
        var repository = new TongouReadRepository(fixture.Context);

        var artist = await repository.GetArtistByIdAsync(1);

        Assert.NotNull(artist);
        Assert.Equal(1, artist!.Id);
        Assert.Equal("Artist One", artist.Name);
        Assert.Null(await repository.GetArtistByIdAsync(999));
    }

    [Fact]
    public async Task GetWorkByIdAsync_maps_atristid_and_name()
    {
        await using var fixture = TestDb.Tongou();
        Seed(fixture.Context);
        var repository = new TongouReadRepository(fixture.Context);

        var work = await repository.GetWorkByIdAsync(10);

        Assert.NotNull(work);
        Assert.Equal(10, work!.Id);
        Assert.Equal(1, work.ArtistId);       // Atristid -> ArtistId
        Assert.Equal("Artist One", work.ArtistName); // AtristidName -> ArtistName
        Assert.Null(await repository.GetWorkByIdAsync(999));
    }

    [Fact]
    public async Task GetWorksByArtistIdAsync_filters_by_atristid()
    {
        await using var fixture = TestDb.Tongou();
        Seed(fixture.Context);
        var repository = new TongouReadRepository(fixture.Context);

        var works = await repository.GetWorksByArtistIdAsync(1);

        Assert.Equal(new[] { 10, 11 }, works.Select(item => item.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetArtistProfileAsync_returns_artist_with_works()
    {
        await using var fixture = TestDb.Tongou();
        Seed(fixture.Context);
        var repository = new TongouReadRepository(fixture.Context);

        var profile = await repository.GetArtistProfileAsync(1);

        Assert.NotNull(profile);
        Assert.Equal(1, profile!.Artist.Id);
        Assert.Equal(new[] { 10, 11 }, profile.Works.Select(item => item.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetArtistProfileAsync_returns_null_when_artist_missing()
    {
        await using var fixture = TestDb.Tongou();
        Seed(fixture.Context);
        var repository = new TongouReadRepository(fixture.Context);

        Assert.Null(await repository.GetArtistProfileAsync(999));
    }

    [Fact]
    public async Task Context_blocks_SaveChanges()
    {
        await using var fixture = TestDb.Tongou();

        Assert.Throws<InvalidOperationException>(() => fixture.Context.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    private static void Seed(TongouLegacyReadContext context)
    {
        InsertArtist(context, 1, "Artist One");
        InsertArtist(context, 2, "Artist Two");

        InsertWork(context, 10, atristId: 1, atristName: "Artist One");
        InsertWork(context, 11, atristId: 1, atristName: "Artist One");
        InsertWork(context, 20, atristId: 2, atristName: "Artist Two");
    }

    private static void InsertArtist(TongouLegacyReadContext context, int id, string name)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO TongouAtrist (id, Name, WorksCount, Aboutid, AboutText, Title, HeardPath, HotCount) " +
            "VALUES (@id, @name, 0, 0, 'about', 'title', 'head.jpg', 0)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@name", name));
    }

    private static void InsertWork(TongouLegacyReadContext context, int id, int atristId, string atristName)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO TongouWorks (id, Atristid, AtristidName, Title, ImgPath, Size, VisityCount, Type, CreationDate, HotCount) " +
            "VALUES (@id, @atristId, @atristName, 'work', 'img.jpg', 'A4', 0, 'oil', '2024', 0)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@atristId", atristId),
            new SqliteParameter("@atristName", atristName));
    }
}
