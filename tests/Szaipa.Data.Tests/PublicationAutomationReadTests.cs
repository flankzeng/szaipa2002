using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 展览自动化（2026-10-26）读侧：首页推荐排序 / 专题积木读取 / 旧库缺列缺表时的降级。
/// 读上下文禁止 SaveChanges，所以全部用原始 SQL 播种。
/// </summary>
public sealed class PublicationAutomationReadTests
{
    // ---------- 首页推荐 ----------

    [Fact]
    public async Task GetHomeFeaturedAsync_returns_only_featured_ordered_by_sort_then_start_desc_then_id_desc()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "无推荐", featured: false, sort: 0);
        InsertPublication(fixture.Context, 2, "排第三", featured: true, sort: 30, start: "2024-01-01");
        InsertPublication(fixture.Context, 3, "排第一", featured: true, sort: 10, start: "2023-01-01");
        InsertPublication(fixture.Context, 4, "同序较新", featured: true, sort: 20, start: "2025-05-05");
        InsertPublication(fixture.Context, 5, "同序较旧", featured: true, sort: 20, start: "2024-05-05");
        InsertPublication(fixture.Context, 6, "同序同日大 id", featured: true, sort: 20, start: "2025-05-05");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = await repository.GetHomeFeaturedAsync();

        Assert.Equal(new[] { 3, 6, 4, 5, 2 }, featured.Select(f => f.Publication.Id).ToArray());
        Assert.Equal(new[] { 10, 20, 20, 20, 30 }, featured.Select(f => f.FeaturedSort).ToArray());
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_skips_offline_exhibitions_even_when_ticked()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "展示", featured: true, sort: 1, status: false);
        InsertPublication(fixture.Context, 2, "下架", featured: true, sort: 0, status: null);
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = await repository.GetHomeFeaturedAsync();

        Assert.Equal(new[] { 1 }, featured.Select(f => f.Publication.Id).ToArray());
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_maps_card_fields_and_splits_image_names_safely()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(
            fixture.Context, 1, "同构", featured: true, sort: 1, folder: "zhongri",
            images: "10015.jpg, 10017.jpg，10038.jpg\n../evil.jpg,a/b.jpg,a\\b.jpg,,10052.jpg");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = Assert.Single(await repository.GetHomeFeaturedAsync());

        Assert.Equal("zhongri", featured.Publication.FolderName);
        Assert.Equal("org", featured.Publication.Organizer);
        Assert.Equal(new[] { "10015.jpg", "10017.jpg", "10038.jpg", "10052.jpg" }, featured.ImageNames.ToArray());
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_returns_empty_when_feature_columns_do_not_exist_yet()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "旧库展览", featured: true, sort: 1);
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN IsFeatured");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = await repository.GetHomeFeaturedAsync();

        Assert.Empty(featured);
    }

    [Fact]
    public async Task Legacy_summary_queries_keep_working_without_feature_columns()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "旧库展览", featured: false, sort: 0);
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN IsFeatured");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN FeaturedSort");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN FeaturedImages");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN ThemeColor");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN BannerPath");
        var repository = new PublicationReadRepository(fixture.Context);

        var list = await repository.GetLatestPublicationsAsync(0);
        var detail = await repository.GetPublicationDetailSnapshotAsync(1, 0);

        Assert.Single(list);
        Assert.NotNull(detail);
        Assert.Equal("旧库展览", detail!.Publication.TitleCn);
    }

    // ---------- 专题积木 ----------

    [Fact]
    public async Task Topic_detail_loads_theme_and_blocks_in_sort_order()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2, theme: "#1a6b5a", banner: "/Content/images/x/banner.jpg");
        InsertBlock(fixture.Context, 1, 7, 1, "quote", """{"text":"B"}""");
        InsertBlock(fixture.Context, 2, 7, 0, "preface", """{"heading":"序","body":"A"}""");
        InsertBlock(fixture.Context, 3, 8, 0, "gallery", "{}"); // 属于其它展览
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(7, 0);

        Assert.NotNull(snapshot);
        var publication = snapshot!.Publication;
        Assert.Equal(PublicationTypes.Topic, publication.Type);
        Assert.Equal("#1a6b5a", publication.ThemeColor);
        Assert.Equal("/Content/images/x/banner.jpg", publication.BannerPath);
        Assert.Equal(new[] { "preface", "quote" }, publication.Blocks.Select(b => b.BlockType).ToArray());
    }

    [Fact]
    public async Task Topic_detail_also_loads_the_works_catalog()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2);
        fixture.Context.Database.ExecuteSqlRaw("INSERT INTO ExhibitionWork (Id, PublicationId, Title, SortOrder) VALUES (1, 7, '作品', 0)");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(7, 0);

        Assert.Single(snapshot!.Publication.Works);
    }

    [Fact]
    public async Task Non_topic_exhibitions_do_not_load_blocks_or_theme()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "普通", type: 0, theme: "#ff0000");
        InsertBlock(fixture.Context, 1, 1, 0, "quote", """{"text":"x"}""");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(1, 0);

        Assert.Empty(snapshot!.Publication.Blocks);
        Assert.Equal(string.Empty, snapshot.Publication.ThemeColor);
    }

    [Fact]
    public async Task Topic_detail_renders_without_blocks_when_the_block_table_is_missing()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2, theme: "#1a6b5a");
        fixture.Context.Database.ExecuteSqlRaw("DROP TABLE PublicationBlock");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(7, 0);

        Assert.NotNull(snapshot);
        Assert.Equal(PublicationTypes.Topic, snapshot!.Publication.Type);
        Assert.Equal("#1a6b5a", snapshot.Publication.ThemeColor);
        Assert.Empty(snapshot.Publication.Blocks);
    }

    [Fact]
    public async Task Topic_detail_falls_back_to_default_theme_when_theme_columns_are_missing()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2);
        InsertBlock(fixture.Context, 1, 7, 0, "quote", """{"text":"x"}""");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN ThemeColor");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN BannerPath");
        var repository = new PublicationReadRepository(fixture.Context);

        var snapshot = await repository.GetPublicationDetailSnapshotAsync(7, 0);

        Assert.NotNull(snapshot);
        Assert.Equal(string.Empty, snapshot!.Publication.ThemeColor);
        Assert.Equal(string.Empty, snapshot.Publication.BannerPath);
        Assert.Single(snapshot.Publication.Blocks);
    }

    [Fact]
    public async Task Topic_detail_other_database_errors_still_propagate()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2);
        // 破坏表结构，使查询报「列不存在」之外的错误（表整体不存在于 Publication 之外是已知降级，这里模拟 Publication 表丢失）
        fixture.Context.Database.ExecuteSqlRaw("DROP TABLE Publication");
        var repository = new PublicationReadRepository(fixture.Context);

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => repository.GetPublicationDetailSnapshotAsync(7, 0));
    }

    private static void InsertPublication(
        SzaipaLegacyReadContext context,
        int id,
        string title,
        bool featured = false,
        int sort = 0,
        string? start = null,
        bool? status = true,
        string folder = "f",
        string? images = null,
        int type = 0,
        string? theme = null,
        string? banner = null)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Publication (Id, TitleCN, MaxImg, ReadCount, Status, FolderName, zhuban, StartDate, [Type], IsFeatured, FeaturedSort, FeaturedImages, ThemeColor, BannerPath) " +
            "VALUES (@id, @title, 0, 0, @status, @folder, 'org', @start, @type, @featured, @sort, @images, @theme, @banner)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@title", title),
            new SqliteParameter("@status", (object?)status ?? DBNull.Value),
            new SqliteParameter("@folder", folder),
            new SqliteParameter("@start", (object?)start ?? DBNull.Value),
            new SqliteParameter("@type", type),
            new SqliteParameter("@featured", featured ? 1 : 0),
            new SqliteParameter("@sort", sort),
            new SqliteParameter("@images", (object?)images ?? DBNull.Value),
            new SqliteParameter("@theme", (object?)theme ?? DBNull.Value),
            new SqliteParameter("@banner", (object?)banner ?? DBNull.Value));
    }

    private static void InsertBlock(SzaipaLegacyReadContext context, int id, int publicationId, int sort, string type, string content)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO PublicationBlock (Id, PublicationId, SortOrder, BlockType, Content) VALUES (@id, @pid, @sort, @type, @content)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@pid", publicationId),
            new SqliteParameter("@sort", sort),
            new SqliteParameter("@type", type),
            new SqliteParameter("@content", content));
    }
}
