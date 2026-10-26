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
    // ---------- 首页推荐（自包含的 FeaturedExhibition 表，不依赖 Publication 行） ----------

    [Fact]
    public async Task GetHomeFeaturedAsync_returns_rows_by_sort_order_including_hidden_ones()
    {
        await using var fixture = TestDb.Szaipa();
        InsertFeatured(fixture.Context, 1, sort: 30, title: "第三", link: "/Home/Publication/92010");
        InsertFeatured(fixture.Context, 2, sort: 10, title: "第一", link: "/Publication/chunyu3");
        InsertFeatured(fixture.Context, 3, sort: 20, title: "隐藏", link: "/Home/Publication/92009", visible: false);
        InsertFeatured(fixture.Context, 4, sort: 20, title: "同序较大 id", link: "/Home/Publication/92011");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = await repository.GetHomeFeaturedAsync();

        Assert.Equal(new[] { 2, 3, 4, 1 }, featured.Select(f => f.Id).ToArray());
        Assert.False(featured.Single(f => f.Id == 3).IsVisible); // 隐藏项仍返回：视图靠它们的链接屏蔽对应硬编码块
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_works_without_any_publication_rows()
    {
        // 生产 Publication 表 0 行：推荐信息必须自包含，指向兼容目录里的固定 id / 手写专题页 URL。
        await using var fixture = TestDb.Szaipa();
        InsertFeatured(fixture.Context, 1, sort: 10, title: "春语III", link: "/Publication/chunyu3");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = Assert.Single(await repository.GetHomeFeaturedAsync());

        Assert.Equal("/Publication/chunyu3", featured.LinkUrl);
        Assert.Equal("春语III", featured.Title);
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_maps_card_fields_and_drops_unsafe_values()
    {
        await using var fixture = TestDb.Szaipa();
        fixture.Context.Database.ExecuteSqlRaw(
            "INSERT INTO FeaturedExhibition (Id, SortOrder, IsVisible, ShowInCarousel, Title, YearText, LinkUrl, InfoLines, ImageUrls, LogoUrl, CoverUrl) VALUES " +
            "(1, 1, 1, 1, '同构', '2024', '/Home/Publication/92011', '开始时间：2024.06.28 \n\n展览地点：东京', " +
            "'/Content/images/zhongri/10015.jpg\nhttps://evil.example/x.jpg\n/Content/../x.jpg\n/Content/images/zhongri/10017.jpg', " +
            "'/Content/images/TongGouJP.png', 'https://evil.example/cover.jpg')");
        fixture.Context.Database.ExecuteSqlRaw(
            "UPDATE FeaturedExhibition SET InfoLines = REPLACE(InfoLines, '\n', char(10)), ImageUrls = REPLACE(ImageUrls, '\n', char(10))");
        InsertFeatured(fixture.Context, 2, sort: 2, title: "外链", link: "https://evil.example/");
        var repository = new PublicationReadRepository(fixture.Context);

        var featured = Assert.Single(await repository.GetHomeFeaturedAsync()); // 外部链接整行丢弃

        Assert.True(featured.ShowInCarousel);
        Assert.Equal("2024", featured.YearText);
        Assert.Equal(new[] { "开始时间：2024.06.28", "展览地点：东京" }, featured.InfoLines.ToArray());
        Assert.Equal(new[] { "/Content/images/zhongri/10015.jpg", "/Content/images/zhongri/10017.jpg" }, featured.ImageUrls.ToArray());
        Assert.Equal("/Content/images/TongGouJP.png", featured.LogoUrl);
        Assert.Equal(string.Empty, featured.CoverUrl);
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_returns_empty_when_the_table_does_not_exist_yet()
    {
        await using var fixture = TestDb.Szaipa();
        fixture.Context.Database.ExecuteSqlRaw("DROP TABLE FeaturedExhibition");
        var repository = new PublicationReadRepository(fixture.Context);

        Assert.Empty(await repository.GetHomeFeaturedAsync());
    }

    [Fact]
    public async Task GetHomeFeaturedAsync_returns_empty_when_optional_lifecycle_columns_are_not_migrated()
    {
        await using var fixture = TestDb.Szaipa();
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE FeaturedExhibition DROP COLUMN Status");
        var repository = new PublicationReadRepository(fixture.Context);

        Assert.Empty(await repository.GetHomeFeaturedAsync());
    }

    [Fact]
    public async Task GetPublicationDetailAsync_returns_empty_topic_blocks_when_optional_block_columns_are_missing()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 7, "专题", type: 2);
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE PublicationBlock DROP COLUMN Content");
        var repository = new PublicationReadRepository(fixture.Context);

        var detail = await repository.GetPublicationDetailSnapshotAsync(7, relatedCount: 0);

        Assert.NotNull(detail);
        Assert.Empty(detail.Publication.Blocks);
    }

    [Fact]
    public async Task Legacy_summary_queries_keep_working_without_any_new_columns()
    {
        await using var fixture = TestDb.Szaipa();
        InsertPublication(fixture.Context, 1, "旧库展览");
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
        // 主表本身丢失不属于「可选新增结构缺失」，必须继续抛出而不是被吞掉。
        fixture.Context.Database.ExecuteSqlRaw("DROP TABLE Publication");
        var repository = new PublicationReadRepository(fixture.Context);

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => repository.GetPublicationDetailSnapshotAsync(7, 0));
    }

    private static void InsertPublication(
        SzaipaLegacyReadContext context,
        int id,
        string title,
        bool? status = true,
        int type = 0,
        string? theme = null,
        string? banner = null)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO Publication (Id, TitleCN, MaxImg, ReadCount, Status, FolderName, zhuban, [Type], ThemeColor, BannerPath) " +
            "VALUES (@id, @title, 0, 0, @status, 'f', 'org', @type, @theme, @banner)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@title", title),
            new SqliteParameter("@status", (object?)status ?? DBNull.Value),
            new SqliteParameter("@type", type),
            new SqliteParameter("@theme", (object?)theme ?? DBNull.Value),
            new SqliteParameter("@banner", (object?)banner ?? DBNull.Value));
    }

    private static void InsertFeatured(SzaipaLegacyReadContext context, int id, int sort, string title, string link, bool visible = true)
    {
        context.Database.ExecuteSqlRaw(
            "INSERT INTO FeaturedExhibition (Id, SortOrder, IsVisible, ShowInCarousel, Title, LinkUrl) VALUES (@id, @sort, @visible, 0, @title, @link)",
            new SqliteParameter("@id", id),
            new SqliteParameter("@sort", sort),
            new SqliteParameter("@visible", visible ? 1 : 0),
            new SqliteParameter("@title", title),
            new SqliteParameter("@link", link));
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
