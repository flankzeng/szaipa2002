using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the News read-only EF Core slice against a SQLite in-memory database that uses the same
/// table/column mapping as the legacy schema. Seeding goes through raw SQL because the context blocks
/// <c>SaveChanges</c>; that also proves the <c>ToTable</c>/column names match what the queries expect.
/// </summary>
public sealed class NewsReadRepositoryTests
{
    // News rows: Date desc => 3 (Newest), 2 (Middle), 1 (Oldest).
    // Important: id1 = false, id2 = true, id3 = null (legacy maps null => Important).
    private const string LongSubtitle =
        "This subtitle is deliberately longer than ninety characters so the landing-page trim rule has something to cut off here.";

    [Fact]
    public async Task GetLatestNewsAsync_orders_by_date_desc_and_caps_to_count()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var result = await repository.GetLatestNewsAsync(2);

        Assert.Equal(new[] { 3, 2 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetLatestNewsAsync_with_zero_count_returns_all_rows()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var result = await repository.GetLatestNewsAsync(0);

        Assert.Equal(new[] { 3, 2, 1 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetLatestNewsAsync_maps_important_with_legacy_null_semantics()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var byId = (await repository.GetLatestNewsAsync(0)).ToDictionary(item => item.Id);

        Assert.False(byId[1].Important); // explicit false
        Assert.True(byId[2].Important);  // explicit true
        Assert.Null(byId[3].Important);  // raw null preserved; each surface applies its own rule
    }

    [Fact]
    public async Task GetLatestNewsAsync_does_not_trim_subtitle()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var middle = (await repository.GetLatestNewsAsync(0)).Single(item => item.Id == 2);

        // newnews does NOT trim; only the landing snapshot does.
        Assert.Equal(LongSubtitle, middle.Subtitle);
    }

    [Fact]
    public async Task GetNewsByIdAsync_projects_detail_including_author_from_Autor()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var detail = await repository.GetNewsByIdAsync(2);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Id);
        Assert.Equal("AuthorB", detail.Author);
        Assert.Equal("Middle", detail.Title);
    }

    [Fact]
    public async Task GetNewsByIdAsync_returns_null_when_missing()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        Assert.Null(await repository.GetNewsByIdAsync(999));
    }

    [Fact]
    public async Task GetRelatedNewsAsync_returns_latest_capped_list()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var related = await repository.GetRelatedNewsAsync(2);

        Assert.Equal(new[] { 3, 2 }, related.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task SearchNewsAsync_matches_title_or_content_ordered_by_date_desc()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var result = await repository.SearchNewsAsync("keyword");

        // Content of id1 ("alpha keyword") and id3 ("keyword gamma") match; ordered Date desc => 3, 1.
        Assert.Equal(new[] { 3, 1 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetHomePageSnapshotAsync_trims_subtitle_caps_news_and_orders_publications()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var snapshot = await repository.GetHomePageSnapshotAsync();

        // Latest news capped to the landing count, Date desc.
        Assert.Equal(LegacyDisplayRules.LandingPageNewsCount, 6);
        Assert.Equal(new[] { 3, 2, 1 }, snapshot.LatestNews.Select(item => item.Id).ToArray());

        // The landing snapshot trims long subtitles to 90 chars + "...".
        var trimmed = snapshot.LatestNews.Single(item => item.Id == 2).Subtitle;
        Assert.Equal(93, trimmed.Length);
        Assert.EndsWith("...", trimmed);

        // Publications ordered by StartDate desc: P2 (2025-06), P1 (2025-01), P3 (2024-01).
        Assert.Equal(new[] { 2, 1, 3 }, snapshot.FeaturedPublications.Select(item => item.Id).ToArray());

        // Status preserved (nullable) and organizer/folder fields mapped.
        var byId = snapshot.FeaturedPublications.ToDictionary(item => item.Id);
        Assert.True(byId[1].Status);
        Assert.False(byId[2].Status);
        Assert.Null(byId[3].Status);
        Assert.Equal("f1", byId[1].FolderName);
        Assert.Equal("org1", byId[1].Organizer);
    }

    [Fact]
    public async Task GetHomePageSnapshotAsync_splits_important_and_normal_news_in_order()
    {
        await using var fixture = CreateSeeded();
        var repository = new NewsReadRepository(fixture.Context);

        var snapshot = await repository.GetHomePageSnapshotAsync();

        // Order preserved from LatestNews (Date desc): important = 3, 2; normal = 1.
        Assert.Equal(new[] { 3, 2 }, snapshot.ImportantNews.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { 1 }, snapshot.NormalNews.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task Context_blocks_SaveChanges()
    {
        await using var fixture = CreateSeeded();

        Assert.Throws<InvalidOperationException>(() => fixture.Context.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Context.SaveChangesAsync());
    }

    private static Fixture CreateSeeded()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SzaipaLegacyReadContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SzaipaLegacyReadContext(options);
        context.Database.EnsureCreated();

        InsertNews(context, 1, "Oldest", "short", "AuthorA", "2026-01-01 00:00:00", "alpha keyword", "1.jpg", important: false);
        InsertNews(context, 2, "Middle", LongSubtitle, "AuthorB", "2026-02-01 00:00:00", "beta", "2.jpg", important: true);
        InsertNews(context, 3, "Newest", "gamma", "AuthorC", "2026-03-01 00:00:00", "keyword gamma", "3.jpg", important: null);

        InsertPublication(context, 1, "P1", "2025-01-01 00:00:00", status: true, folderName: "f1", organizer: "org1");
        InsertPublication(context, 2, "P2", "2025-06-01 00:00:00", status: false, folderName: "f2", organizer: "org2");
        InsertPublication(context, 3, "P3", "2024-01-01 00:00:00", status: null, folderName: "f3", organizer: "org3");

        return new Fixture(context, connection);
    }

    private static void InsertNews(
        SzaipaLegacyReadContext context,
        int id,
        string title,
        string subtitle,
        string autor,
        string date,
        string content,
        string coverPath,
        bool? important)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO News (Id, Title, Subtitle, Autor, original, Date, Content, CoverPath, ReadCount, Important) " +
            "VALUES (@id, @title, @subtitle, @autor, 0, @date, @content, @cover, 0, @important)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@title", title),
            new SqliteParameter("@subtitle", subtitle),
            new SqliteParameter("@autor", autor),
            new SqliteParameter("@date", date),
            new SqliteParameter("@content", content),
            new SqliteParameter("@cover", coverPath),
            new SqliteParameter("@important", (object?)important ?? DBNull.Value));
    }

    private static void InsertPublication(
        SzaipaLegacyReadContext context,
        int id,
        string titleCn,
        string startDate,
        bool? status,
        string folderName,
        string organizer)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Publication (Id, TitleCN, StartDate, MaxImg, ReadCount, Status, FolderName, zhuban) " +
            "VALUES (@id, @titleCn, @startDate, 0, 0, @status, @folderName, @zhuban)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@titleCn", titleCn),
            new SqliteParameter("@startDate", startDate),
            new SqliteParameter("@status", (object?)status ?? DBNull.Value),
            new SqliteParameter("@folderName", folderName),
            new SqliteParameter("@zhuban", organizer));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(SzaipaLegacyReadContext context, SqliteConnection connection)
        {
            Context = context;
            Connection = connection;
        }

        public SzaipaLegacyReadContext Context { get; }

        private SqliteConnection Connection { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            Connection.Dispose();
        }
    }
}
