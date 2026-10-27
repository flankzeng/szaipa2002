using System.Data.Common;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.WeChat;

/// <summary>公众号文章 → News 的导入编排（链接批量导入 / 重新抓取 / 官方接口同步）。</summary>
public interface IWeChatSyncService
{
    /// <summary>同步记录表是否已建立。</summary>
    Task<bool> IsTableAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 批量链接导入：<paramref name="rawInput"/> 可含多行/空格/逗号分隔的多条链接。
    /// <paramref name="source"/> 写入同步记录的 Source（后台粘贴为 link，群发回调为 callback）。
    /// </summary>
    Task<IReadOnlyList<WeChatImportOutcome>> ImportLinksAsync(
        string rawInput, AdminActor actor, bool force, CancellationToken cancellationToken, string source = WeChatSources.Link);

    /// <summary>按同步记录的 SourceUrl 重新抓取页面并强制覆盖对应新闻。</summary>
    Task<WeChatImportOutcome> ReimportAsync(int recordId, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// 官方接口同步（发布记录 + 永久图文素材，各自独立开关、独立失败）：<paramref name="full"/>=true 回填全部历史，
    /// 否则每个来源只看最新两页。接口只负责"发现文章链接"，正文一律抓页面、按 biz/mid/idx 归一去重。
    /// </summary>
    Task<WeChatSyncSummary> SyncFromApiAsync(bool full, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>把一篇已取得的文章写入 News（两条通道共用的导入器）。</summary>
    Task<WeChatImportOutcome> ImportArticleAsync(WeChatArticle article, AdminActor actor, bool force, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IWeChatSyncService" />
public sealed class WeChatSyncService : IWeChatSyncService
{
    public const string PageClientName = "wechat-page";
    public const int MaxLinksPerBatch = 50;

    /// <summary>后台定时同步与手动"立即同步"互斥，避免同时回填。</summary>
    private static readonly SemaphoreSlim ApiSyncGate = new(1, 1);

    private const int MaxPageBytes = 20 * 1024 * 1024;
    private const int SubtitleMaxLength = 200;

    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _recorder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWeChatApiClient _apiClient;
    private readonly WeChatImageLocalizer _images;
    private readonly IOptionsMonitor<WeChatOptions> _options;
    private readonly ILogger<WeChatSyncService> _logger;
    private readonly IWeChatSourceStatusStore _status;
    private readonly WeChatContentCleaner _cleaner;
    private DateTime _lastPageFetchUtc = DateTime.MinValue;

    public WeChatSyncService(
        SzaipaAdminContext db,
        IOperationRecorder recorder,
        IHttpClientFactory httpClientFactory,
        IWeChatApiClient apiClient,
        WeChatImageLocalizer images,
        IOptionsMonitor<WeChatOptions> options,
        ILogger<WeChatSyncService> logger,
        IWeChatSourceStatusStore status)
    {
        _db = db;
        _recorder = recorder;
        _httpClientFactory = httpClientFactory;
        _apiClient = apiClient;
        _images = images;
        _options = options;
        _logger = logger;
        _status = status;
        _cleaner = new WeChatContentCleaner(options.CurrentValue);
    }

    public async Task<bool> IsTableAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.WeChatSyncRecord.AsNoTracking().AnyAsync(cancellationToken);
            return true;
        }
        catch (DbException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ 链接导入

    public async Task<IReadOnlyList<WeChatImportOutcome>> ImportLinksAsync(
        string rawInput, AdminActor actor, bool force, CancellationToken cancellationToken, string source = WeChatSources.Link)
    {
        var inputs = ParseLinkInputs(rawInput);

        var results = new List<WeChatImportOutcome>();
        if (inputs.Count == 0)
        {
            return results;
        }

        if (!await IsTableAvailableAsync(cancellationToken))
        {
            return new[] { new WeChatImportOutcome(string.Empty, WeChatImportStatus.Failed, null, null, WeChatSchema.MissingTableMessage) };
        }

        if (inputs.Count > MaxLinksPerBatch)
        {
            results.Add(new WeChatImportOutcome(
                string.Empty, WeChatImportStatus.Failed, null, null,
                $"一次最多导入 {MaxLinksPerBatch} 条链接，其余 {inputs.Count - MaxLinksPerBatch} 条已忽略，请分批提交。"));
            inputs = inputs.Take(MaxLinksPerBatch).ToList();
        }

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = WeChatSourceKey.NormalizeArticleUrl(input);
            if (url is null)
            {
                results.Add(new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, null,
                    "不是有效的公众号文章链接（需形如 https://mp.weixin.qq.com/s/xxxx）。"));
                continue;
            }

            if (!force)
            {
                // 长链接能直接算出 biz+mid+idx 键时按键判重（同一篇带/不带 chksm 都能命中）；短链只能比 URL。
                var key = WeChatSourceKey.TryFromLongUrl(input);
                var known = await _db.WeChatSyncRecord.AsNoTracking()
                    .Where(r => key != null ? r.SourceKey == key : r.SourceUrl == url)
                    .Select(r => new { r.NewsId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (known is not null)
                {
                    results.Add(new WeChatImportOutcome(input, WeChatImportStatus.Unchanged, known.NewsId, null, "该文章已导入过（如需覆盖请用「重新抓取」）。"));
                    continue;
                }
            }

            await PoliteDelayAsync(cancellationToken);
            results.Add(await ImportOneLinkAsync(input, url, actor, force, source, cancellationToken));
        }

        RecordLinkStatus(source, results);
        return results;
    }

    private static List<string> ParseLinkInputs(string? rawInput)
    {
        var inputs = new List<string>();
        foreach (var segment in (rawInput ?? string.Empty)
                     .Split(new[] { '\r', '\n', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = segment.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var whitespaceParts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // 接受同一行以空格/制表符分隔的多条合法链接；若行内包含自然语言或无效 token，
            // 保留整行作为单个输入，从而给出一个清楚的格式错误，而不是拆成多个噪声错误。
            if (whitespaceParts.Length > 1 && whitespaceParts.All(part => WeChatSourceKey.NormalizeArticleUrl(part) is not null))
            {
                inputs.AddRange(whitespaceParts);
            }
            else
            {
                inputs.Add(line);
            }
        }

        return inputs.Distinct(StringComparer.Ordinal).ToList();
    }

    private void RecordLinkStatus(string source, IReadOnlyList<WeChatImportOutcome> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        var ok = results.Count(r => r.Status != WeChatImportStatus.Failed);
        if (ok == 0)
        {
            _status.RecordFailure(source, results.First(r => r.Message is not null).Message!);
            return;
        }

        _status.RecordSuccess(
            source,
            $"新增 {results.Count(r => r.Status == WeChatImportStatus.Created)}，更新 {results.Count(r => r.Status == WeChatImportStatus.Updated)}，无变化 {results.Count(r => r.Status == WeChatImportStatus.Unchanged)}，失败 {results.Count(r => r.Status == WeChatImportStatus.Failed)}");
    }

    /// <summary>两次抓取文章页之间至少间隔 FetchDelayMilliseconds，对微信公开页保持礼貌。</summary>
    private async Task PoliteDelayAsync(CancellationToken cancellationToken)
    {
        var delay = _options.CurrentValue.FetchDelayMilliseconds;
        if (delay > 0)
        {
            var wait = _lastPageFetchUtc.AddMilliseconds(delay) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
        }

        _lastPageFetchUtc = DateTime.UtcNow;
    }

    public async Task<WeChatImportOutcome> ReimportAsync(int recordId, AdminActor actor, CancellationToken cancellationToken)
    {
        WeChatSyncRecord? record;
        try
        {
            record = await _db.WeChatSyncRecord.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        }
        catch (DbException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            return new WeChatImportOutcome(string.Empty, WeChatImportStatus.Failed, null, null, WeChatSchema.MissingTableMessage);
        }

        var url = WeChatSourceKey.NormalizeArticleUrl(record?.SourceUrl);
        if (record is null || url is null)
        {
            return new WeChatImportOutcome(string.Empty, WeChatImportStatus.Failed, null, null, "未找到同步记录，或该记录没有可抓取的文章链接。");
        }

        return await ImportOneLinkAsync(url, url, actor, force: true, WeChatSources.Link, cancellationToken);
    }

    private async Task<WeChatImportOutcome> ImportOneLinkAsync(
        string input, string url, AdminActor actor, bool force, string source, CancellationToken cancellationToken)
    {
        try
        {
            var html = await FetchPageAsync(url, cancellationToken);
            var article = WeChatArticlePageParser.Parse(html, url) with { Source = source };
            return (await ImportArticleAsync(article, actor, force, cancellationToken)) with { Input = input };
        }
        catch (WeChatParseException exception)
        {
            return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, null, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("抓取公众号文章失败 {Type}", exception.GetType().Name);
            return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, null, "抓取文章页失败（网络不通或被微信拒绝），请稍后重试。");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, null, "抓取文章页超时，请稍后重试。");
        }
    }

    private async Task<string> FetchPageAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(PageClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxPageBytes)
        {
            throw new WeChatParseException("文章页过大，已放弃。");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length > MaxPageBytes)
        {
            throw new WeChatParseException("文章页过大，已放弃。");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    // ------------------------------------------------------------------ 官方接口同步

    public async Task<WeChatSyncSummary> SyncFromApiAsync(bool full, AdminActor actor, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue; // 有效配置：后台设置页保存后下一次调用即生效
        if (!options.IsApiConfigured)
        {
            return WeChatSyncSummary.Failure("官方接口未启用：请配置 WeChat:Enabled、AppId、AppSecret。");
        }

        var sources = new List<(string Id, Func<int, int, CancellationToken, Task<WeChatSourcePage>> Fetch)>();
        if (options.Sources.FreePublish)
        {
            sources.Add((WeChatSources.FreePublish, _apiClient.GetPublishedPageAsync));
        }

        if (options.Sources.Material)
        {
            sources.Add((WeChatSources.Material, _apiClient.GetMaterialNewsPageAsync));
        }

        if (sources.Count == 0)
        {
            return WeChatSyncSummary.Failure("两个接口来源（发布记录 / 永久图文素材）都已在配置里关闭。");
        }

        if (!await IsTableAvailableAsync(cancellationToken))
        {
            return WeChatSyncSummary.Failure(WeChatSchema.MissingTableMessage);
        }

        if (!await ApiSyncGate.WaitAsync(0, cancellationToken))
        {
            return WeChatSyncSummary.Failure("已有一个公众号同步任务正在运行，请稍后再试。");
        }

        try
        {
            var results = new List<WeChatSourceRunResult>();
            foreach (var (id, fetch) in sources)
            {
                // 来源之间互不影响：一个来源报 48001/40164 只记录它自己的错误，继续下一个。
                results.Add(await RunSourceAsync(id, fetch, full, actor, cancellationToken));
            }

            return new WeChatSyncSummary(results, null);
        }
        finally
        {
            ApiSyncGate.Release();
        }
    }

    private async Task<WeChatSourceRunResult> RunSourceAsync(
        string sourceId,
        Func<int, int, CancellationToken, Task<WeChatSourcePage>> fetchPage,
        bool full,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        int fetched = 0, created = 0, updated = 0, unchanged = 0, failed = 0;
        const int pageSize = 20;
        const int incrementalPages = 2;
        try
        {
            var offset = 0;
            for (var page = 0; full || page < incrementalPages; page++)
            {
                var result = await fetchPage(offset, pageSize, cancellationToken);
                foreach (var item in result.Articles)
                {
                    fetched++;
                    var outcome = await ProcessDiscoveredAsync(item, sourceId, actor, cancellationToken);
                    switch (outcome.Status)
                    {
                        case WeChatImportStatus.Created: created++; break;
                        case WeChatImportStatus.Updated: updated++; break;
                        case WeChatImportStatus.Unchanged: unchanged++; break;
                        default:
                            failed++;
                            _logger.LogWarning("公众号文章同步失败（{Source}）：{Message}", sourceId, outcome.Message);
                            break;
                    }
                }

                offset += result.ItemCount;
                if (result.ItemCount == 0 || offset >= result.TotalCount)
                {
                    break;
                }
            }

            _status.RecordSuccess(sourceId, $"拉取 {fetched}，新增 {created}，更新 {updated}，无变化 {unchanged}，失败 {failed}");
            return new WeChatSourceRunResult(sourceId, fetched, created, updated, unchanged, failed, null);
        }
        catch (WeChatApiException exception)
        {
            _logger.LogWarning("公众号来源 {Source} 同步中断：{Message}", sourceId, exception.Message);
            _status.RecordFailure(sourceId, exception.Message);
            return new WeChatSourceRunResult(sourceId, fetched, created, updated, unchanged, failed, exception.Message);
        }
    }

    /// <summary>
    /// 接口发现的一条文章：已有同 biz/mid/idx 的记录则直接跳过（不抓页面）；否则抓页面解析，
    /// 页面不可用时退回接口自带正文。
    /// </summary>
    private async Task<WeChatImportOutcome> ProcessDiscoveredAsync(
        WeChatDiscoveredArticle item, string sourceId, AdminActor actor, CancellationToken cancellationToken)
    {
        var url = WeChatSourceKey.NormalizeArticleUrl(item.Url);
        if (url is null)
        {
            return new WeChatImportOutcome(item.Url, WeChatImportStatus.Failed, null, item.Title, "接口返回的文章链接无法识别。");
        }

        var key = WeChatSourceKey.TryFromLongUrl(item.Url);
        if (key is not null)
        {
            var known = await _db.WeChatSyncRecord.AsNoTracking()
                .Where(r => r.SourceKey == key)
                .Select(r => new { r.NewsId })
                .FirstOrDefaultAsync(cancellationToken);
            if (known is not null)
            {
                return new WeChatImportOutcome(url, WeChatImportStatus.Unchanged, known.NewsId, item.Title, "已导入。");
            }
        }

        await PoliteDelayAsync(cancellationToken);
        var outcome = await ImportOneLinkAsync(url, url, actor, force: false, sourceId, cancellationToken);
        if (outcome.Status == WeChatImportStatus.Failed
            && !string.IsNullOrWhiteSpace(item.ContentHtml)
            && !string.IsNullOrWhiteSpace(item.Title))
        {
            var fallback = new WeChatArticle
            {
                SourceKey = key ?? WeChatSourceKey.FromApi(item.ArticleId, item.ItemIndex),
                SourceUrl = url,
                Title = item.Title!,
                Author = item.Author,
                Digest = item.Digest,
                CoverUrl = item.CoverUrl,
                PublishedAt = item.PublishedAt,
                ContentHtml = item.ContentHtml!,
                ArticleId = item.ArticleId,
                ItemIndex = item.ItemIndex,
                Source = sourceId
            };
            outcome = await ImportArticleAsync(fallback, actor, force: false, cancellationToken);
        }

        return outcome;
    }

    // ------------------------------------------------------------------ 导入核心

    public async Task<WeChatImportOutcome> ImportArticleAsync(
        WeChatArticle article, AdminActor actor, bool force, CancellationToken cancellationToken)
    {
        var input = article.SourceUrl;
        try
        {
            // 1) 第一遍清洗（保留远程图片地址）：算哈希、收集图片清单。无网络、无写盘。
            var remote = _cleaner.Clean(article.ContentHtml, article.Title);
            if (remote.Html.Length == 0)
            {
                return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, article.Title, "清洗后正文为空（可能是纯视频/纯图文卡片文章）。");
            }

            var hash = ComputeHash(article, remote.Html);
            var record = await _db.WeChatSyncRecord.FirstOrDefaultAsync(r => r.SourceKey == article.SourceKey, cancellationToken);
            News? existingNews = null;
            if (record is not null)
            {
                existingNews = await _db.News.FirstOrDefaultAsync(n => n.Id == record.NewsId, cancellationToken);
                if (!force && existingNews is not null && record.ContentHash == hash)
                {
                    return new WeChatImportOutcome(input, WeChatImportStatus.Unchanged, record.NewsId, article.Title, "文章没有变化。");
                }
            }

            if (!_images.IsAvailable)
            {
                return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, article.Title,
                    "未配置可写的内容目录（LegacyAssets:ContentRoot），无法把文章图片保存到本站。");
            }

            // 2) 图片本地化（正文图 + 封面）。
            var publishedAt = article.PublishedAt ?? existingNews?.Date ?? DateTime.Now;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var imageUrl in remote.ImageUrls)
            {
                var local = await _images.LocalizeBodyImageAsync(imageUrl, publishedAt, cancellationToken);
                if (local is not null)
                {
                    map[imageUrl] = local.Url;
                }
            }

            string? coverFile = null;
            var coverRemote = _cleaner.ResolveImageUrl(article.CoverUrl);
            if (coverRemote is not null)
            {
                coverFile = (await _images.LocalizeCoverAsync(coverRemote, cancellationToken))?.FileName;
            }

            if (coverFile is null && remote.ImageUrls.Count > 0)
            {
                // 没有（或下不来）封面：退而用正文第一张图。
                coverFile = (await _images.LocalizeCoverAsync(remote.ImageUrls[0], cancellationToken))?.FileName;
            }

            // 3) 第二遍清洗：把图片地址改写为本地 URL，失败的图丢弃（绝不热链）。
            var final = _cleaner.Clean(article.ContentHtml, article.Title, url => map.GetValueOrDefault(url));

            // 4) 落库：News + 同步记录，同一事务。
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.Now;
            var created = existingNews is null;
            var news = existingNews ?? new News();
            var author = !string.IsNullOrWhiteSpace(article.Author) ? article.Author!.Trim() : article.AccountName?.Trim();

            news.Title = article.Title;
            news.Subtitle = BuildSubtitle(article.Digest, final.PlainText);
            news.Content = final.Html;
            news.Autor = string.IsNullOrEmpty(author) ? "公众号" : author;
            news.original = false; // legacy 语义：original=false = 转载/有外部来源，link 存来源地址
            news.link = article.SourceUrl;
            if (coverFile is not null)
            {
                news.CoverPath = coverFile;
            }

            if (created)
            {
                news.Date = publishedAt;
                news.Year = publishedAt.Year;
                news.ReadCount = 0;
                news.Important = _options.CurrentValue.NewArticlesImportant;
                news.EditRecord = $"公众号同步 于 {now:yyyy年MM月dd日 HH:mm:ss} 导入了此新闻条目。/";
                _db.News.Add(news);
            }
            else
            {
                news.EditRecord += $"公众号同步 于 {now:yyyy年MM月dd日 HH:mm:ss} 更新了此新闻条目。/";
            }

            await _db.SaveChangesAsync(cancellationToken); // 取得 news.Id

            if (record is null)
            {
                record = new WeChatSyncRecord { SourceKey = article.SourceKey, IsHidden = false };
                _db.WeChatSyncRecord.Add(record);
            }

            record.SourceUrl = article.SourceUrl;
            record.ArticleId = article.ArticleId;
            record.ItemIndex = article.ItemIndex;
            record.NewsId = news.Id;
            record.ContentHash = hash;
            record.SyncedAt = now;
            record.Source = article.Source;

            await _recorder.RecordAsync(
                _db,
                actor,
                created ? $"从公众号导入了 {news.Title}的新闻条目。" : $"从公众号更新了 {news.Title}的新闻条目。",
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new WeChatImportOutcome(
                input,
                created ? WeChatImportStatus.Created : WeChatImportStatus.Updated,
                news.Id,
                news.Title,
                created ? "已导入。" : "已更新。");
        }
        catch (DbException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            _db.ChangeTracker.Clear();
            return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, article.Title, WeChatSchema.MissingTableMessage);
        }
        catch (DbUpdateException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            _db.ChangeTracker.Clear();
            return new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, article.Title, WeChatSchema.MissingTableMessage);
        }
        catch (DbUpdateException exception)
        {
            // 最常见的是并发下 SourceKey 唯一索引冲突（另一路已导入同一篇）。
            _db.ChangeTracker.Clear();
            _logger.LogWarning("公众号文章落库失败 {Type}", exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            var existing = await _db.WeChatSyncRecord.AsNoTracking()
                .Where(r => r.SourceKey == article.SourceKey)
                .Select(r => new { r.NewsId })
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null
                ? new WeChatImportOutcome(input, WeChatImportStatus.Unchanged, existing.NewsId, article.Title, "该文章刚刚已被导入。")
                : new WeChatImportOutcome(input, WeChatImportStatus.Failed, null, article.Title, "写入数据库失败（本地库是否可写？）。");
        }
    }

    internal static string ComputeHash(WeChatArticle article, string cleanedHtml)
    {
        var text = string.Join('\n', article.Title, article.Digest, article.CoverUrl, cleanedHtml);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string BuildSubtitle(string? digest, string plainText)
    {
        var text = !string.IsNullOrWhiteSpace(digest) ? digest.Trim() : plainText;
        text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= SubtitleMaxLength ? text : text[..SubtitleMaxLength];
    }
}
