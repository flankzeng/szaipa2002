using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 前台新闻列表的可见性：隐藏的同步文章、旧手录新闻（Id &lt;= LegacyNewsMaxId 且无同步记录）不在
/// 首页/列表/侧栏/搜索出现；详情按 Id 仍可访问；同步记录表不存在时降级为显示全部。
/// 数据：Id 1-3 = 旧手录；Id 4 = 同步（显示）；Id 5 = 同步（隐藏）；Id 6 = 上线后后台新建（无记录）。
/// </summary>
public sealed class NewsReadWeChatVisibilityTests
{
    private static NewsReadRepository Repository(ContextFixture<SzaipaLegacyReadContext> fixture, int? legacyMax) =>
        new(fixture.Context, WeChatTestSupport.Opts(new WeChatOptions { LegacyNewsMaxId = legacyMax }));

    [Fact]
    public async Task Without_legacy_max_only_hidden_sync_articles_are_excluded()
    {
        await using var fixture = Seed();

        var list = await Repository(fixture, null).GetLatestNewsAsync(0);

        Assert.Equal(new[] { 6, 4, 3, 2, 1 }, list.Select(n => n.Id).ToArray());
    }

    [Fact]
    public async Task With_legacy_max_old_manual_news_are_hidden_but_new_manual_and_synced_remain()
    {
        await using var fixture = Seed();
        var repository = Repository(fixture, 3);

        var list = await repository.GetLatestNewsAsync(0);
        var home = await repository.GetHomePageSnapshotAsync();
        var related = await repository.GetRelatedNewsAsync(5);
        var search = await repository.SearchNewsAsync("正文");

        Assert.Equal(new[] { 6, 4 }, list.Select(n => n.Id).ToArray());
        Assert.Equal(new[] { 6, 4 }, home.LatestNews.Select(n => n.Id).ToArray());
        Assert.Equal(new[] { 6, 4 }, related.Select(n => n.Id).ToArray());
        Assert.Equal(new[] { 6, 4 }, search.Select(n => n.Id).ToArray());
    }

    [Fact]
    public async Task Synced_article_with_a_record_below_the_legacy_max_still_shows()
    {
        await using var fixture = Seed();

        // 把分界设得比同步文章 Id(4) 还高：它有同步记录，所以仍然显示；无记录的 6 也在分界内被隐藏。
        var list = await Repository(fixture, 6).GetLatestNewsAsync(0);

        Assert.Equal(new[] { 4 }, list.Select(n => n.Id).ToArray());
    }

    [Fact]
    public async Task Detail_pages_stay_reachable_for_legacy_and_hidden_news()
    {
        await using var fixture = Seed();
        var repository = Repository(fixture, 3);

        Assert.NotNull(await repository.GetNewsByIdAsync(1)); // 旧手录
        Assert.NotNull(await repository.GetNewsByIdAsync(5)); // 隐藏的同步文章
    }

    [Fact]
    public async Task Missing_sync_table_degrades_to_showing_every_news()
    {
        await using var fixture = Seed();
        await fixture.Context.Database.ExecuteSqlRawAsync("DROP TABLE WeChatSyncRecord");
        var repository = Repository(fixture, 3);

        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, (await repository.GetLatestNewsAsync(0)).Select(n => n.Id).ToArray());
        Assert.Equal(6, (await repository.GetHomePageSnapshotAsync()).LatestNews.Count);
        Assert.Equal(6, (await repository.GetRelatedNewsAsync(0)).Count);
        Assert.Equal(6, (await repository.SearchNewsAsync("正文")).Count);
    }

    [Fact]
    public async Task Repository_without_wechat_options_keeps_the_original_behaviour()
    {
        await using var fixture = Seed();
        var repository = new NewsReadRepository(fixture.Context);

        Assert.Equal(new[] { 6, 4, 3, 2, 1 }, (await repository.GetLatestNewsAsync(0)).Select(n => n.Id).ToArray());
    }

    private static ContextFixture<SzaipaLegacyReadContext> Seed()
    {
        var fixture = TestDb.Szaipa();
        var context = fixture.Context;
        for (var id = 1; id <= 6; id++)
        {
            context.Database.ExecuteSqlRaw(
                "INSERT INTO News (Id, Title, Subtitle, Autor, original, Date, Content, CoverPath, ReadCount, Important) " +
                $"VALUES ({id}, 'N{id}', 's', 'a', 0, '2026-0{id}-01 00:00:00', '正文{id}', '{id}.jpg', 0, 0)");
        }

        context.Database.ExecuteSqlRaw(
            "INSERT INTO WeChatSyncRecord (SourceKey, NewsId, ContentHash, SyncedAt, IsHidden, Source, ItemIndex) " +
            "VALUES ('mp:b:1:1', 4, 'h', '2026-04-01 00:00:00', 0, 'link', 0), ('mp:b:2:1', 5, 'h', '2026-05-01 00:00:00', 1, 'link', 0)");
        return fixture;
    }
}
