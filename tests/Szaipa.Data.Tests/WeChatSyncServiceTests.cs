using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 导入器端到端（SQLite 内存库 + 假 HTTP + 假图片存储）：字段映射、去重/增量、图片改写、
/// 缺表降级、来源互不影响。全程不访问外网。
/// </summary>
public sealed class WeChatSyncServiceTests
{
    private static readonly AdminActor Actor = new(1, "tester");
    private const string ShortUrl = "https://mp.weixin.qq.com/s/NoiseSample01";
    private const string LongUrl = "https://mp.weixin.qq.com/s?__biz=MzI5NDg4MDAyMw%3D%3D&mid=2247490001&idx=1&sn=sn1";

    // ---------------------------------------------------------------- 字段映射 + 图片本地化

    [Fact]
    public async Task Link_import_maps_fields_into_the_existing_news_structure_and_localizes_images()
    {
        await using var env = new Env();

        var results = await env.Service.ImportLinksAsync(ShortUrl, Actor, force: false, CancellationToken.None);

        var outcome = Assert.Single(results);
        Assert.Equal(WeChatImportStatus.Created, outcome.Status);

        var news = await env.Db.News.AsNoTracking().SingleAsync();
        Assert.Equal(outcome.NewsId, news.Id);
        Assert.Equal("同构第三季开幕 & 艺术家座谈", news.Title);
        Assert.Equal("展览将持续至十二月。", news.Subtitle);                       // ← 摘要
        Assert.Equal("深圳市艺术产业促进会", news.Autor);                           // 作者为空 → 公众号名
        Assert.False(news.original);                                                // legacy 语义：有外部来源 = 转载
        Assert.Equal(ShortUrl, news.link);                                          // ← 公众号原文
        Assert.Equal(new DateTime(2025, 10, 26, 17, 40, 0), news.Date);             // ← ct 时间戳（北京时间）
        Assert.Equal(2025, news.Year);
        Assert.Equal(0, news.ReadCount);
        Assert.False(news.Important);
        Assert.Contains("公众号同步", news.EditRecord);

        // 封面：落在 newsImg，CoverPath 只存文件名（与后台 News 上传封面同存法）。
        Assert.Matches("^wx-[0-9a-f]{32}\\.png$", news.CoverPath);
        Assert.True(env.Store.Files.ContainsKey($"newsImg/{news.CoverPath}"));

        // 正文：图片改写为 /Content/WeChat/2025/10/{hash}.png，且不热链、无内联样式。
        Assert.Matches("src=\"/Content/WeChat/2025/10/[0-9a-f]{32}\\.png\"", news.Content);
        Assert.DoesNotContain("qpic.cn", news.Content);
        Assert.DoesNotContain("font-size", news.Content);
        Assert.Equal(2, env.Store.Files.Keys.Count(k => k.StartsWith("WeChat/2025/10/", StringComparison.Ordinal)));

        var record = await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync();
        Assert.Equal("mp:MzI5NDg4MDAyMw==:2247490001:1", record.SourceKey);
        Assert.Equal(news.Id, record.NewsId);
        Assert.Equal(ShortUrl, record.SourceUrl);
        Assert.Equal("link", record.Source);
        Assert.False(record.IsHidden);
        Assert.Equal(64, record.ContentHash.Length);

        // 审计：写入 Diary 操作记录。
        Assert.Contains("从公众号导入了 同构第三季开幕 & 艺术家座谈的新闻条目", (await env.Db.Diary.AsNoTracking().SingleAsync()).OperationRecord);
    }

    [Fact]
    public async Task New_articles_can_be_marked_important_by_configuration()
    {
        await using var env = new Env(new WeChatOptions { NewArticlesImportant = true });

        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);

        Assert.True((await env.Db.News.AsNoTracking().SingleAsync()).Important);
    }

    [Fact]
    public async Task Failed_image_downloads_are_dropped_not_hotlinked()
    {
        await using var env = new Env { ImageStatus = System.Net.HttpStatusCode.NotFound };

        var outcome = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None));

        Assert.Equal(WeChatImportStatus.Created, outcome.Status);
        var news = await env.Db.News.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("<img", news.Content);
        Assert.DoesNotContain("qpic.cn", news.Content);
        Assert.Null(news.CoverPath);
        Assert.Empty(env.Store.Files);
    }

    [Fact]
    public async Task Non_image_responses_are_rejected_by_magic_number()
    {
        await using var env = new Env { ImageBytes = System.Text.Encoding.UTF8.GetBytes("<html>not an image at all</html>") };

        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);

        Assert.Empty(env.Store.Files);
        Assert.DoesNotContain("<img", (await env.Db.News.AsNoTracking().SingleAsync()).Content);
    }

    [Fact]
    public async Task Image_localizer_rejects_userinfo_nondefault_ports_and_unlisted_hosts_before_request()
    {
        await using var env = new Env();

        Assert.Null(await env.ImageLocalizer.LocalizeBodyImageAsync("https://user@mmbiz.qpic.cn/a/640", DateTime.UtcNow, CancellationToken.None));
        Assert.Null(await env.ImageLocalizer.LocalizeBodyImageAsync("https://mmbiz.qpic.cn:8443/a/640", DateTime.UtcNow, CancellationToken.None));
        Assert.Null(await env.ImageLocalizer.LocalizeBodyImageAsync("https://evil.example.com/a/640", DateTime.UtcNow, CancellationToken.None));
        Assert.Empty(env.Handler.Requests);
    }

    [Fact]
    public async Task Image_redirect_is_not_followed_or_retried()
    {
        await using var env = new Env { ImageRedirectTo = "https://evil.example.com/stolen.png" };

        var image = await env.ImageLocalizer.LocalizeBodyImageAsync("https://mmbiz.qpic.cn/a/redirect/640", DateTime.UtcNow, CancellationToken.None);

        Assert.Null(image);
        Assert.Single(env.Handler.Requests);
        Assert.Contains("mmbiz.qpic.cn", env.Handler.Requests[0]);
        Assert.DoesNotContain("evil.example.com", env.Handler.Requests[0]);
    }

    [Fact]
    public async Task Import_fails_with_a_clear_message_when_no_writable_content_root()
    {
        await using var env = new Env();
        env.Store.IsAvailable = false;

        var outcome = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None));

        Assert.Equal(WeChatImportStatus.Failed, outcome.Status);
        Assert.Contains("LegacyAssets:ContentRoot", outcome.Message);
        Assert.Empty(await env.Db.News.ToListAsync());
    }

    // ---------------------------------------------------------------- 去重 / 增量

    [Fact]
    public async Task Importing_the_same_article_again_is_unchanged_and_downloads_nothing()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var imageRequests = env.Handler.Requests.Count(r => r.Contains("qpic.cn"));

        // 同一链接再次粘贴：直接按 SourceUrl 跳过，连页面都不抓。
        var again = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None));
        Assert.Equal(WeChatImportStatus.Unchanged, again.Status);

        // 用 force（重新抓取）：抓页面但哈希相同 → 不再下载图片、不新增新闻。
        var forced = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, force: true, CancellationToken.None));
        Assert.Equal(WeChatImportStatus.Updated, forced.Status);

        Assert.Equal(1, await env.Db.News.CountAsync());
        Assert.Equal(1, await env.Db.WeChatSyncRecord.CountAsync());
        Assert.Equal(imageRequests, env.Handler.Requests.Count(r => r.Contains("qpic.cn"))); // 图片已在本地，复用
    }

    [Fact]
    public async Task Short_and_long_urls_of_the_same_article_create_only_one_news()
    {
        await using var env = new Env();
        env.Pages[LongUrl] = env.Pages[ShortUrl];

        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var second = Assert.Single(await env.Service.ImportLinksAsync(LongUrl, Actor, false, CancellationToken.None));

        Assert.Equal(WeChatImportStatus.Unchanged, second.Status); // 页面脚本里的 biz+mid+idx 对上了同一条记录
        Assert.Equal(1, await env.Db.News.CountAsync());
        Assert.Equal(1, await env.Db.WeChatSyncRecord.CountAsync());
    }

    [Fact]
    public async Task Long_links_are_fetched_with_chksm_and_deduped_by_key_without_refetching()
    {
        // 真实微信对去掉 chksm 的长链接会 302 到人机验证页，所以抓取必须带原样 chksm；token/lang/#rd 这类会话参数去掉。
        await using var env = new Env();
        env.Pages[LongUrl] = env.Pages[ShortUrl];

        var first = Assert.Single(await env.Service.ImportLinksAsync(
            LongUrl + "&chksm=c0ffee&token=1006457716&lang=zh_CN#rd", Actor, false, CancellationToken.None));
        Assert.Equal(WeChatImportStatus.Created, first.Status);
        var pageRequest = Assert.Single(env.Handler.Requests, r => r.Contains("mp.weixin.qq.com"));
        Assert.Contains("chksm=c0ffee", pageRequest);
        Assert.DoesNotContain("token=", pageRequest);

        // 同一篇再贴一次（这次不带 chksm）：按 biz+mid+idx 键直接判重，不再请求页面。
        var again = Assert.Single(await env.Service.ImportLinksAsync(LongUrl, Actor, false, CancellationToken.None));
        Assert.Equal(WeChatImportStatus.Unchanged, again.Status);
        Assert.Single(env.Handler.Requests, r => r.Contains("mp.weixin.qq.com"));
        Assert.Equal(1, await env.Db.News.CountAsync());
    }

    [Fact]
    public async Task Changed_source_article_updates_the_same_news_and_keeps_its_date_and_flags()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var before = await env.Db.News.AsNoTracking().SingleAsync();
        var hashBefore = (await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync()).ContentHash;
        var pinned = await env.Db.News.SingleAsync(news => news.Id == before.Id);
        pinned.Important = true; // 后台置顶写入 News.Important；同步更新不得覆盖。
        await env.Db.SaveChangesAsync();

        env.Pages[ShortUrl] = env.Pages[ShortUrl].Replace("感谢各位到场。", "感谢各位到场，作品已更新。");
        var outcome = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, force: true, CancellationToken.None));

        Assert.Equal(WeChatImportStatus.Updated, outcome.Status);
        env.Db.ChangeTracker.Clear();
        var after = await env.Db.News.AsNoTracking().SingleAsync();
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Date, after.Date);
        Assert.True(after.Important); // 后台置顶不会被同步冲掉
        Assert.Contains("作品已更新", after.Content);
        Assert.Contains("更新了此新闻条目", after.EditRecord);
        Assert.NotEqual(hashBefore, (await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync()).ContentHash);
    }

    [Fact]
    public async Task Deleted_news_is_recreated_under_the_same_record()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        env.Db.News.RemoveRange(await env.Db.News.ToListAsync());
        await env.Db.SaveChangesAsync();

        var outcome = Assert.Single(await env.Service.ImportLinksAsync(ShortUrl, Actor, force: true, CancellationToken.None));

        Assert.Equal(WeChatImportStatus.Created, outcome.Status);
        Assert.Equal(1, await env.Db.WeChatSyncRecord.CountAsync());
        Assert.Equal(outcome.NewsId, (await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync()).NewsId);
    }

    // ---------------------------------------------------------------- 输入校验 / 失败

    [Fact]
    public async Task Batch_import_reports_each_line_and_never_requests_foreign_hosts()
    {
        await using var env = new Env();
        env.Pages["https://mp.weixin.qq.com/s/DeletedOne"] = "<html><body>该内容已被发布者删除</body></html>";
        var input = string.Join(Environment.NewLine, new[] { ShortUrl, "https://evil.example.com/s/abc", "not a link", "https://mp.weixin.qq.com/s/DeletedOne", ShortUrl });

        var results = await env.Service.ImportLinksAsync(input, Actor, false, CancellationToken.None);

        Assert.True(results.Count == 4, string.Join(" | ", results.Select(result => $"{result.Input}:{result.Status}:{result.Message}"))); // 重复行去重
        Assert.Equal(WeChatImportStatus.Created, results[0].Status);
        Assert.Equal(WeChatImportStatus.Failed, results[1].Status);
        Assert.Equal(WeChatImportStatus.Failed, results[2].Status);
        Assert.Contains("已被发布者删除", results[3].Message);
        Assert.DoesNotContain(env.Handler.Requests, r => r.Contains("evil.example.com"));
        Assert.Equal(1, await env.Db.News.CountAsync());
    }

    [Fact]
    public async Task Batch_import_accepts_space_separated_links_and_keeps_natural_language_together()
    {
        await using var env = new Env();
        env.Pages[LongUrl] = env.Pages[ShortUrl];

        var results = await env.Service.ImportLinksAsync($"{ShortUrl} {LongUrl}{Environment.NewLine}not a link", Actor, false, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(WeChatImportStatus.Created, results[0].Status);
        Assert.Equal(WeChatImportStatus.Unchanged, results[1].Status);
        Assert.Equal("not a link", results[2].Input);
        Assert.Equal(WeChatImportStatus.Failed, results[2].Status);
        Assert.Equal(1, await env.Db.News.CountAsync());
    }

    [Fact]
    public async Task More_than_the_batch_limit_is_cut_with_a_notice()
    {
        await using var env = new Env();
        var input = string.Join('\n', Enumerable.Range(0, WeChatSyncService.MaxLinksPerBatch + 5).Select(i => $"https://mp.weixin.qq.com/s/Missing{i}"));

        var results = await env.Service.ImportLinksAsync(input, Actor, false, CancellationToken.None);

        Assert.Equal(WeChatSyncService.MaxLinksPerBatch + 1, results.Count);
        Assert.Contains("最多导入", results[0].Message);
    }

    [Fact]
    public async Task Missing_sync_table_gives_the_run_sql_message_and_never_throws()
    {
        await using var env = new Env();
        await env.Db.Database.ExecuteSqlRawAsync("DROP TABLE WeChatSyncRecord");

        var results = await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var directly = await env.Service.ImportArticleAsync(
            WeChatArticlePageParser.Parse(env.Pages[ShortUrl], ShortUrl), Actor, false, CancellationToken.None);

        Assert.False(await env.Service.IsTableAvailableAsync(CancellationToken.None));
        Assert.Contains("wechat-sync.sql", Assert.Single(results).Message);
        Assert.Equal(WeChatImportStatus.Failed, directly.Status);
        Assert.Contains("wechat-sync.sql", directly.Message);
        Assert.False((await new WeChatSyncAdminRepository(env.Db, new OperationRecorder()).GetPagedAsync(1, 10, CancellationToken.None)).TableAvailable);
        Assert.Equal(0, await env.Db.News.CountAsync());
    }

    // ---------------------------------------------------------------- 官方接口来源

    [Fact]
    public async Task Api_sources_are_independent_one_failing_does_not_block_the_other()
    {
        await using var env = new Env(Configured());
        env.Pages[LongUrl] = env.Pages[ShortUrl];
        env.Api.FreePublish.Enqueue(new WeChatSourcePage(1, 1, new[] { Discovered(LongUrl, "头条") }));
        env.Api.MaterialError = new WeChatApiException(WeChatApiClient.DescribeError(48001, null), 48001);

        var summary = await env.Service.SyncFromApiAsync(full: false, Actor, CancellationToken.None);

        Assert.Null(summary.Error);
        var freepublish = summary.Sources.Single(s => s.SourceId == WeChatSources.FreePublish);
        var material = summary.Sources.Single(s => s.SourceId == WeChatSources.Material);
        Assert.Equal(1, freepublish.Created);
        Assert.Null(freepublish.Error);
        Assert.Contains("未开通", material.Error);

        var status = env.Status.Snapshot(Configured());
        Assert.NotNull(status.Single(s => s.SourceId == WeChatSources.FreePublish).LastSuccessAt);
        Assert.Contains("48001", status.Single(s => s.SourceId == WeChatSources.Material).LastError);
        Assert.Equal("freepublish", (await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync()).Source);
    }

    [Fact]
    public async Task Api_item_already_known_by_biz_mid_idx_is_skipped_without_fetching_the_page()
    {
        await using var env = new Env(Configured());
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None); // 先用链接导入
        var requestsBefore = env.Handler.Requests.Count;
        env.Api.FreePublish.Enqueue(new WeChatSourcePage(1, 1, new[] { Discovered(LongUrl, "头条") }));

        var summary = await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None);

        Assert.Equal(1, summary.Sources.Single(s => s.SourceId == WeChatSources.FreePublish).Unchanged);
        Assert.Equal(requestsBefore, env.Handler.Requests.Count);       // 不抓页面、不下图
        Assert.Equal(1, await env.Db.News.CountAsync());                // 不同来源拿到同一篇只建一条
    }

    [Fact]
    public async Task Api_item_falls_back_to_api_content_when_the_page_cannot_be_fetched()
    {
        await using var env = new Env(Configured());
        const string url = "https://mp.weixin.qq.com/s?__biz=MzI5&mid=77&idx=2&sn=zz";
        env.Api.FreePublish.Enqueue(new WeChatSourcePage(1, 1, new[]
        {
            new WeChatDiscoveredArticle(url, "接口标题", "作者乙", "接口摘要", null, "<p>接口里的正文内容</p>",
                new DateTime(2025, 5, 6, 7, 8, 9), "ART9", 1)
        }));

        var summary = await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None); // 假页面返回 404

        Assert.Equal(1, summary.Sources.Single(s => s.SourceId == WeChatSources.FreePublish).Created);
        var news = await env.Db.News.AsNoTracking().SingleAsync();
        Assert.Equal("接口标题", news.Title);
        Assert.Equal("作者乙", news.Autor);
        Assert.Equal(new DateTime(2025, 5, 6, 7, 8, 9), news.Date);
        Assert.Equal("<p>接口里的正文内容</p>", news.Content);
        var record = await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync();
        Assert.Equal("mp:MzI5:77:2", record.SourceKey);
        Assert.Equal("ART9", record.ArticleId);
    }

    [Fact]
    public async Task Incremental_sync_reads_two_pages_full_backfill_reads_until_the_end()
    {
        await using var env = new Env(Configured(materialOff: true));
        for (var i = 0; i < 5; i++)
        {
            env.Api.FreePublish.Enqueue(new WeChatSourcePage(100, 20, Array.Empty<WeChatDiscoveredArticle>()));
        }

        await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None);
        Assert.Equal(new[] { 0, 20 }, env.Api.FreePublishOffsets.ToArray());

        env.Api.FreePublishOffsets.Clear();
        for (var i = 0; i < 6; i++)
        {
            env.Api.FreePublish.Enqueue(new WeChatSourcePage(100, 20, Array.Empty<WeChatDiscoveredArticle>()));
        }

        await env.Service.SyncFromApiAsync(true, Actor, CancellationToken.None);
        Assert.Equal(new[] { 0, 20, 40, 60, 80 }, env.Api.FreePublishOffsets.ToArray());
    }

    [Fact]
    public async Task Api_sync_is_refused_when_not_configured_or_all_sources_are_off_or_table_missing()
    {
        await using (var env = new Env())
        {
            Assert.Contains("未启用", (await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None)).Error);
        }

        await using (var env = new Env(Configured(freePublishOff: true, materialOff: true)))
        {
            Assert.Contains("都已", (await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None)).Error);
        }

        await using (var env = new Env(Configured()))
        {
            await env.Db.Database.ExecuteSqlRawAsync("DROP TABLE WeChatSyncRecord");
            Assert.Contains("wechat-sync.sql", (await env.Service.SyncFromApiAsync(false, Actor, CancellationToken.None)).Error);
        }
    }

    // ---------------------------------------------------------------- 后台：隐藏 / 置顶 / 列表

    [Fact]
    public async Task Admin_repository_lists_hides_and_pins_and_audits()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var repository = new WeChatSyncAdminRepository(env.Db, new OperationRecorder());
        var record = await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync();

        Assert.True(await repository.SetHiddenAsync(record.Id, true, Actor, CancellationToken.None));
        Assert.True(await repository.SetPinnedAsync(record.Id, true, Actor, CancellationToken.None));
        env.Db.ChangeTracker.Clear();

        var list = await repository.GetPagedAsync(1, 10, CancellationToken.None);
        var item = Assert.Single(list.Page.Items);
        Assert.True(list.TableAvailable);
        Assert.True(item.IsHidden);
        Assert.True(item.IsPinned);
        Assert.True(item.NewsExists);
        Assert.Equal("同构第三季开幕 & 艺术家座谈", item.Title);
        var diary = (await env.Db.Diary.AsNoTracking().SingleAsync()).OperationRecord;
        Assert.Contains("隐藏了公众号同步新闻", diary);
        Assert.Contains("置顶了公众号同步新闻", diary);

        env.Db.News.RemoveRange(await env.Db.News.ToListAsync());
        await env.Db.SaveChangesAsync();
        var afterDelete = Assert.Single((await repository.GetPagedAsync(1, 10, CancellationToken.None)).Page.Items);
        Assert.False(afterDelete.NewsExists);
        Assert.False(await repository.SetPinnedAsync(record.Id, false, Actor, CancellationToken.None));
        Assert.False(await repository.SetHiddenAsync(9999, true, Actor, CancellationToken.None));
    }

    [Fact]
    public async Task Fixture_import_flows_to_public_news_and_hidden_news_stays_reachable_by_id()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        await using var readDb = new SzaipaLegacyReadContext(
            new DbContextOptionsBuilder<SzaipaLegacyReadContext>().UseSqlite(env.Connection).Options);
        var publicNews = new NewsReadRepository(readDb, WeChatTestSupport.Opts(new WeChatOptions { LegacyNewsMaxId = 5000 }));

        var created = Assert.Single(await publicNews.GetLatestNewsAsync(0));
        Assert.Equal("同构第三季开幕 & 艺术家座谈", created.Title);
        Assert.NotNull(await publicNews.GetNewsByIdAsync(created.Id));

        var record = await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync();
        var admin = new WeChatSyncAdminRepository(env.Db, new OperationRecorder());
        Assert.True(await admin.SetHiddenAsync(record.Id, true, Actor, CancellationToken.None));
        Assert.Empty(await publicNews.GetLatestNewsAsync(0));
        Assert.NotNull(await publicNews.GetNewsByIdAsync(created.Id)); // 隐藏不退役旧详情 URL

        Assert.True(await admin.SetHiddenAsync(record.Id, false, Actor, CancellationToken.None));
        Assert.True(await admin.SetPinnedAsync(record.Id, true, Actor, CancellationToken.None));
        Assert.True((await publicNews.GetLatestNewsAsync(0)).Single().Important);
    }

    [Fact]
    public async Task Reimport_refetches_the_recorded_url_and_overwrites()
    {
        await using var env = new Env();
        await env.Service.ImportLinksAsync(ShortUrl, Actor, false, CancellationToken.None);
        var record = await env.Db.WeChatSyncRecord.AsNoTracking().SingleAsync();
        env.Pages[ShortUrl] = env.Pages[ShortUrl].Replace("感谢各位到场。", "重新抓取后的文字。");

        var outcome = await env.Service.ReimportAsync(record.Id, Actor, CancellationToken.None);

        Assert.Equal(WeChatImportStatus.Updated, outcome.Status);
        env.Db.ChangeTracker.Clear();
        Assert.Contains("重新抓取后的文字。", (await env.Db.News.AsNoTracking().SingleAsync()).Content);
        Assert.Equal(WeChatImportStatus.Failed, (await env.Service.ReimportAsync(9999, Actor, CancellationToken.None)).Status);
    }

    // ---------------------------------------------------------------- 图片落盘辅助

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0 }, ".gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    public void Image_type_is_decided_by_magic_number(byte[] data, string expected) =>
        Assert.Equal(expected, WeChatImageLocalizer.SniffExtension(data));

    [Fact]
    public void Image_hash_ignores_the_query_string_but_not_the_path()
    {
        var a = WeChatImageLocalizer.HashOf(new Uri("https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=jpeg&wxfrom=5"));
        var b = WeChatImageLocalizer.HashOf(new Uri("https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=png"));
        var c = WeChatImageLocalizer.HashOf(new Uri("https://mmbiz.qpic.cn/mmbiz_jpg/BBB/640?wx_fmt=jpeg"));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(32, a.Length);
    }

    // ---------------------------------------------------------------- 测试环境

    private static WeChatOptions Configured(bool freePublishOff = false, bool materialOff = false) => new()
    {
        Enabled = true,
        AppId = "wxtest",
        AppSecret = "secret-not-real",
        FetchDelayMilliseconds = 0,
        Sources = { FreePublish = !freePublishOff, Material = !materialOff }
    };

    private static WeChatDiscoveredArticle Discovered(string url, string title) =>
        new(url, title, null, null, null, "<p>接口正文</p>", null, "A", 0);

    private sealed class FakeApi : IWeChatApiClient
    {
        public Queue<WeChatSourcePage> FreePublish { get; } = new();
        public Queue<WeChatSourcePage> Material { get; } = new();
        public WeChatApiException? MaterialError { get; set; }
        public List<int> FreePublishOffsets { get; } = new();

        public Task VerifyCredentialsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WeChatSourcePage> GetPublishedPageAsync(int offset, int count, CancellationToken cancellationToken)
        {
            FreePublishOffsets.Add(offset);
            return Task.FromResult(FreePublish.Count > 0 ? FreePublish.Dequeue() : new WeChatSourcePage(0, 0, Array.Empty<WeChatDiscoveredArticle>()));
        }

        public Task<WeChatSourcePage> GetMaterialNewsPageAsync(int offset, int count, CancellationToken cancellationToken) =>
            MaterialError is not null
                ? Task.FromException<WeChatSourcePage>(MaterialError)
                : Task.FromResult(Material.Count > 0 ? Material.Dequeue() : new WeChatSourcePage(0, 0, Array.Empty<WeChatDiscoveredArticle>()));

        public Task<WeChatMaterialCount> GetMaterialCountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WeChatMaterialCount(0, 0, 0, 0));
    }

    private sealed class Env : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public Env(WeChatOptions? options = null)
        {
            options ??= new WeChatOptions();
            options.FetchDelayMilliseconds = 0;
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            Db = new SzaipaAdminContext(new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(_connection).Options);
            Db.Database.EnsureCreated();

            Pages[ShortUrl] = WeChatTestSupport.Fixture("wechat-article-noise.html");
            Handler = new FakeHandler(Respond);
            var factory = new FakeHttpClientFactory(Handler);
            var wrapped = WeChatTestSupport.Opts(options);
            Status = new WeChatSourceStatusStore();
            ImageLocalizer = new WeChatImageLocalizer(factory, Store, wrapped, NullLogger<WeChatImageLocalizer>.Instance);
            Service = new WeChatSyncService(
                Db,
                new OperationRecorder(),
                factory,
                Api,
                ImageLocalizer,
                wrapped,
                NullLogger<WeChatSyncService>.Instance,
                Status);
        }

        public SzaipaAdminContext Db { get; }
        public SqliteConnection Connection => _connection;
        public FakeHandler Handler { get; }
        public FakeImageStore Store { get; } = new();
        public FakeApi Api { get; } = new();
        public WeChatSourceStatusStore Status { get; }
        public WeChatSyncService Service { get; }
        public WeChatImageLocalizer ImageLocalizer { get; }
        public Dictionary<string, string> Pages { get; } = new(StringComparer.Ordinal);
        public System.Net.HttpStatusCode ImageStatus { get; set; } = System.Net.HttpStatusCode.OK;
        public byte[] ImageBytes { get; set; } = WeChatTestSupport.PngBytes;
        public string? ImageRedirectTo { get; set; }

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var uri = request.RequestUri!;
            if (uri.Host == "mp.weixin.qq.com")
            {
                var key = uri.AbsolutePath == "/s" ? LongUrl.Contains(uri.Query[..Math.Min(uri.Query.Length, 20)]) ? LongUrl : uri.ToString() : uri.GetLeftPart(UriPartial.Path);
                return Pages.TryGetValue(key, out var html) || Pages.TryGetValue(uri.ToString(), out html)
                    ? FakeHandler.Html(html)
                    : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            if (uri.Host.EndsWith("qpic.cn", StringComparison.Ordinal))
            {
                if (ImageRedirectTo is not null)
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Redirect)
                    {
                        Headers = { Location = new Uri(ImageRedirectTo) }
                    };
                }

                return ImageStatus == System.Net.HttpStatusCode.OK
                    ? FakeHandler.Bytes(ImageBytes)
                    : new HttpResponseMessage(ImageStatus);
            }

            throw new InvalidOperationException("测试不得访问外部地址：" + uri.Host);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _connection.Dispose();
        }
    }
}
