using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 专题积木仓储（替换式保存 / 排序 / 规范化 / 作用域）+ 展览仓储新增的推荐、主题、状态筛选、删除级联、旧库降级。
/// </summary>
public sealed class PublicationBlockAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");
    private static readonly DateTime Today = new(2026, 10, 26);

    // ---------- 积木仓储 ----------

    [Fact]
    public async Task ReplaceAsync_inserts_blocks_in_order_with_contiguous_sort_order()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());

        var saved = await repository.ReplaceAsync(
            1,
            new[]
            {
                new PublicationBlockInput("banner", """{"title":"开篇"}"""),
                new PublicationBlockInput("preface", """{"heading":"序","body":"正文"}"""),
                new PublicationBlockInput("quote", """{"text":"引言"}""")
            },
            Actor,
            CancellationToken.None);

        Assert.Equal(3, saved);
        var rows = await repository.GetByPublicationAsync(1, CancellationToken.None);
        Assert.Equal(new[] { "banner", "preface", "quote" }, rows.Select(r => r.BlockType).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, rows.Select(r => r.SortOrder).ToArray());
        Assert.Contains("开篇", rows[0].Content);
    }

    [Fact]
    public async Task ReplaceAsync_reorders_and_drops_blocks_on_a_second_save()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(
            1,
            new[]
            {
                new PublicationBlockInput("quote", """{"text":"一"}"""),
                new PublicationBlockInput("quote", """{"text":"二"}"""),
                new PublicationBlockInput("quote", """{"text":"三"}""")
            },
            Actor,
            CancellationToken.None);

        await repository.ReplaceAsync(
            1,
            new[]
            {
                new PublicationBlockInput("quote", """{"text":"三"}"""),
                new PublicationBlockInput("quote", """{"text":"一"}""")
            },
            Actor,
            CancellationToken.None);

        var rows = await repository.GetByPublicationAsync(1, CancellationToken.None);
        Assert.Equal(2, rows.Count);
        Assert.Contains("三", rows[0].Content);
        Assert.Contains("一", rows[1].Content);
        Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.SortOrder).ToArray());
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_list_clears_the_page()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(1, new[] { new PublicationBlockInput("gallery", "{}") }, Actor, CancellationToken.None);

        var saved = await repository.ReplaceAsync(1, Array.Empty<PublicationBlockInput>(), Actor, CancellationToken.None);

        Assert.Equal(0, saved);
        Assert.Empty(await repository.GetByPublicationAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task ReplaceAsync_is_scoped_to_one_publication()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());
        await repository.ReplaceAsync(1, new[] { new PublicationBlockInput("gallery", "{}") }, Actor, CancellationToken.None);
        await repository.ReplaceAsync(2, new[] { new PublicationBlockInput("works", "{}") }, Actor, CancellationToken.None);

        await repository.ReplaceAsync(1, Array.Empty<PublicationBlockInput>(), Actor, CancellationToken.None);

        Assert.Empty(await repository.GetByPublicationAsync(1, CancellationToken.None));
        Assert.Equal("works", Assert.Single(await repository.GetByPublicationAsync(2, CancellationToken.None)).BlockType);
    }

    [Fact]
    public async Task ReplaceAsync_drops_unknown_types_and_sanitizes_content()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());

        var saved = await repository.ReplaceAsync(
            1,
            new[]
            {
                new PublicationBlockInput("script", """{"x":"y"}"""),
                new PublicationBlockInput("richtext", """{"html":"<p>安全</p><script>alert(1)</script>"}"""),
                new PublicationBlockInput(null, "{}")
            },
            Actor,
            CancellationToken.None);

        Assert.Equal(1, saved);
        var row = Assert.Single(await repository.GetByPublicationAsync(1, CancellationToken.None));
        Assert.Equal("richtext", row.BlockType);
        Assert.Contains("安全", row.Content);
        Assert.DoesNotContain("script", row.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplaceAsync_caps_the_number_of_blocks()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());
        var items = Enumerable.Range(0, PublicationBlockAdminRepository.MaxBlocksPerPublication + 10)
            .Select(i => new PublicationBlockInput("quote", $$"""{"text":"{{i}}"}"""))
            .ToArray();

        var saved = await repository.ReplaceAsync(1, items, Actor, CancellationToken.None);

        Assert.Equal(PublicationBlockAdminRepository.MaxBlocksPerPublication, saved);
    }

    [Fact]
    public async Task ReplaceAsync_writes_the_operation_record()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());

        await repository.ReplaceAsync(5, new[] { new PublicationBlockInput("gallery", "{}") }, Actor, CancellationToken.None);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Contains("专题（Id=5）", diary.OperationRecord);
    }

    // ---------- 展览仓储：推荐 / 主题 ----------

    [Fact]
    public async Task Create_and_update_persist_featured_and_theme_fields()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new Publication
            {
                TitleCN = "专题展",
                Type = PublicationTypes.Topic,
                IsFeatured = true,
                FeaturedSort = 3,
                FeaturedImages = "a.jpg， b.jpg\n../c.jpg",
                ThemeColor = "#1A6B5A",
                BannerPath = "/Content/images/x/banner.jpg"
            },
            Actor,
            CancellationToken.None);

        var saved = await fixture.Context.Publication.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.True(saved.IsFeatured);
        Assert.Equal(3, saved.FeaturedSort);
        Assert.Equal("a.jpg,b.jpg", saved.FeaturedImages); // 规范化：逗号分隔、去掉路径穿越项
        Assert.Equal("#1a6b5a", saved.ThemeColor);
        Assert.Equal("/Content/images/x/banner.jpg", saved.BannerPath);

        await repository.UpdateAsync(
            new Publication { Id = id, TitleCN = "专题展", Type = PublicationTypes.Topic, IsFeatured = false, FeaturedSort = 0, ThemeColor = "not-a-colour", BannerPath = "https://evil.example/x.jpg" },
            Actor,
            CancellationToken.None);

        var updated = await fixture.Context.Publication.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.False(updated.IsFeatured);
        Assert.Null(updated.FeaturedImages);
        Assert.Null(updated.ThemeColor);   // 非法颜色被丢弃，回退默认主题色
        Assert.Null(updated.BannerPath);   // 非站内路径被丢弃
    }

    [Fact]
    public async Task DeleteAsync_removes_blocks_and_works_of_the_publication_only()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        var blocks = new PublicationBlockAdminRepository(fixture.Context, new OperationRecorder());
        var keep = await repository.CreateAsync(new Publication { TitleCN = "保留" }, Actor, CancellationToken.None);
        var drop = await repository.CreateAsync(new Publication { TitleCN = "删除" }, Actor, CancellationToken.None);
        await blocks.ReplaceAsync(keep, new[] { new PublicationBlockInput("gallery", "{}") }, Actor, CancellationToken.None);
        await blocks.ReplaceAsync(drop, new[] { new PublicationBlockInput("gallery", "{}") }, Actor, CancellationToken.None);
        fixture.Context.ExhibitionWork.Add(new ExhibitionWork { PublicationId = drop, Title = "作品" });
        await fixture.Context.SaveChangesAsync();

        Assert.True(await repository.DeleteAsync(drop, Actor, CancellationToken.None));

        Assert.Empty(await fixture.Context.PublicationBlock.AsNoTracking().Where(b => b.PublicationId == drop).ToListAsync());
        Assert.Empty(await fixture.Context.ExhibitionWork.AsNoTracking().Where(w => w.PublicationId == drop).ToListAsync());
        Assert.Single(await fixture.Context.PublicationBlock.AsNoTracking().Where(b => b.PublicationId == keep).ToListAsync());
    }

    // ---------- 展览仓储：状态筛选 / 计数 / 降级 ----------

    [Fact]
    public async Task GetPagedAsync_filters_by_lifecycle_and_counts_match()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        await Create(repository, "即将", true, "2026-11-01", "2026-12-01");
        await Create(repository, "展中", true, "2026-10-01", "2026-12-01");
        await Create(repository, "展中2", true, null, null);
        await Create(repository, "结束(日期)", true, "2026-01-01", "2026-02-01");
        await Create(repository, "结束(手动)", false, "2026-10-01", "2026-12-01");
        await Create(repository, "下架", null, "2026-10-01", "2026-12-01");

        async Task<string[]> Titles(PublicationLifecycle? lifecycle) =>
            (await repository.GetPagedAsync(1, 50, lifecycle, Today, CancellationToken.None))
                .Items.Select(p => p.TitleCN!).OrderBy(t => t, StringComparer.Ordinal).ToArray();

        static string[] Expect(params string[] titles) => titles.OrderBy(t => t, StringComparer.Ordinal).ToArray();

        Assert.Equal(Expect("即将"), await Titles(PublicationLifecycle.Upcoming));
        Assert.Equal(Expect("展中", "展中2"), await Titles(PublicationLifecycle.Ongoing));
        Assert.Equal(Expect("结束(日期)", "结束(手动)"), await Titles(PublicationLifecycle.Ended));
        Assert.Equal(Expect("下架"), await Titles(PublicationLifecycle.Offline));
        Assert.Equal(6, (await Titles(null)).Length);

        var counts = await repository.GetLifecycleCountsAsync(Today, CancellationToken.None);
        Assert.Equal(1, counts[PublicationLifecycle.Upcoming]);
        Assert.Equal(2, counts[PublicationLifecycle.Ongoing]);
        Assert.Equal(2, counts[PublicationLifecycle.Ended]);
        Assert.Equal(1, counts[PublicationLifecycle.Offline]);
    }

    [Fact]
    public async Task GetPagedAsync_total_count_respects_the_lifecycle_filter()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        for (var i = 0; i < 5; i++)
        {
            await Create(repository, $"展中{i}", true, "2026-10-01", "2026-12-01");
        }

        await Create(repository, "结束", false, null, null);

        var page = await repository.GetPagedAsync(1, 2, PublicationLifecycle.Ongoing, Today, CancellationToken.None);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task GetPagedAsync_still_lists_exhibitions_when_new_columns_are_missing()
    {
        await using var fixture = CreateContext();
        var repository = new PublicationAdminRepository(fixture.Context, new OperationRecorder());
        await Create(repository, "旧库展览", true, "2026-10-01", "2026-12-01");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN IsFeatured");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN FeaturedSort");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN FeaturedImages");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN ThemeColor");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN BannerPath");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN Type");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN Preface");
        fixture.Context.Database.ExecuteSqlRaw("ALTER TABLE Publication DROP COLUMN Signature");

        var page = await repository.GetPagedAsync(1, 20, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal("旧库展览", item.TitleCN);
        Assert.False(item.IsFeatured);
        Assert.Equal(0, item.Type);
    }

    private static Task<int> Create(IPublicationAdminRepository repository, string title, bool? status, string? start, string? end) =>
        repository.CreateAsync(
            new Publication
            {
                TitleCN = title,
                Status = status,
                StartDate = start is null ? null : DateTime.Parse(start),
                EndDate = end is null ? null : DateTime.Parse(end)
            },
            Actor,
            CancellationToken.None);

    private static AdminContextFixture CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options;
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
