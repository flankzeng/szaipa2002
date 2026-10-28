using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.WeChat;

/// <summary>后台同步记录列表的一行（记录 + 对应新闻的展示字段）。</summary>
public sealed record WeChatSyncListItem(
    int RecordId,
    int NewsId,
    string? SourceUrl,
    string Source,
    DateTime SyncedAt,
    bool IsHidden,
    bool NewsExists,
    string? Title,
    DateTime? NewsDate,
    bool IsPinned);

/// <summary>列表结果；<see cref="TableAvailable"/>=false 表示还没执行建表 SQL。</summary>
public sealed record WeChatSyncListResult(bool TableAvailable, PagedResult<WeChatSyncListItem> Page);

/// <summary>公众号同步记录的后台读写（列表 / 隐藏 / 展示封面）。导入本身见 <see cref="IWeChatSyncService"/>。</summary>
public interface IWeChatSyncAdminRepository
{
    Task<WeChatSyncListResult> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>隐藏/取消隐藏：隐藏后文章不再出现在前台列表、首页与侧栏推荐，详情 URL 仍可访问。</summary>
    Task<bool> SetHiddenAsync(int recordId, bool hidden, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>展示封面 = 把对应 News.Important 置为 true（前台渲染为带封面的"重要新闻"大卡片）。</summary>
    Task<bool> SetPinnedAsync(int recordId, bool pinned, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>
    /// 批量补标：每次群发里序号最小的那篇（头条；单篇群发即它本身）若有封面且尚未标重要，则标为重要。
    /// 只会把 false/null 改成 true，不会取消任何已勾选的文章。返回本次新标的篇数。
    /// </summary>
    Task<int> MarkLeadArticlesImportantAsync(AdminActor actor, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class WeChatSyncAdminRepository : IWeChatSyncAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _recorder;

    public WeChatSyncAdminRepository(SzaipaAdminContext db, IOperationRecorder recorder)
    {
        _db = db;
        _recorder = recorder;
    }

    public async Task<WeChatSyncListResult> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        try
        {
            var query = _db.WeChatSyncRecord.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);

            // 左连接 News：新闻被后台删除后记录仍在，列表里显示"新闻已删除"。
            var items = await (
                from record in query
                join news in _db.News.AsNoTracking() on record.NewsId equals news.Id into joined
                from news in joined.DefaultIfEmpty()
                orderby record.SyncedAt descending, record.Id descending
                select new
                {
                    record.Id,
                    record.NewsId,
                    record.SourceUrl,
                    record.Source,
                    record.SyncedAt,
                    record.IsHidden,
                    NewsId2 = (int?)news.Id,
                    Title = news.Title,
                    NewsDate = news.Date,
                    Important = news.Important
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new WeChatSyncListResult(true, new PagedResult<WeChatSyncListItem>
            {
                Items = items.Select(x => new WeChatSyncListItem(
                    x.Id, x.NewsId, x.SourceUrl, x.Source, x.SyncedAt, x.IsHidden,
                    x.NewsId2 is not null, x.Title, x.NewsDate, x.Important == true)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (DbException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            return new WeChatSyncListResult(false, new PagedResult<WeChatSyncListItem> { Page = 1, PageSize = pageSize });
        }
    }

    public async Task<bool> SetHiddenAsync(int recordId, bool hidden, AdminActor actor, CancellationToken cancellationToken)
    {
        var record = await _db.WeChatSyncRecord.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record is null)
        {
            return false;
        }

        record.IsHidden = hidden;
        var title = await _db.News.AsNoTracking().Where(n => n.Id == record.NewsId).Select(n => n.Title).FirstOrDefaultAsync(cancellationToken);
        await _recorder.RecordAsync(_db, actor, $"{(hidden ? "隐藏" : "取消隐藏")}了公众号同步新闻 {title}。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetPinnedAsync(int recordId, bool pinned, AdminActor actor, CancellationToken cancellationToken)
    {
        var record = await _db.WeChatSyncRecord.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record is null)
        {
            return false;
        }

        var news = await _db.News.FirstOrDefaultAsync(n => n.Id == record.NewsId, cancellationToken);
        if (news is null)
        {
            return false;
        }

        news.Important = pinned;
        news.EditRecord += $"{actor.StaffName} 于 {DateTime.Now:yyyy年MM月dd日 HH:mm:ss} {(pinned ? "设为展示封面（重要）" : "取消展示封面")}了此新闻条目。/";
        await _recorder.RecordAsync(_db, actor, $"{(pinned ? "设为展示封面" : "取消展示封面")}了公众号同步新闻 {news.Title}。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkLeadArticlesImportantAsync(AdminActor actor, CancellationToken cancellationToken)
    {
        // 隐藏的文章不参与"谁是头条"的判断：头条被隐藏/删除时，同次群发里下一篇可见的文章顶上。
        var records = await _db.WeChatSyncRecord.AsNoTracking()
            .Where(r => !r.IsHidden)
            .Select(r => new { r.SourceKey, r.ItemIndex, r.NewsId })
            .ToListAsync(cancellationToken);
        var existingNewsIds = (await _db.News.AsNoTracking()
            .Where(n => n.CoverPath != null && n.CoverPath != "")
            .Select(n => n.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var leadNewsIds = records
            .Where(r => existingNewsIds.Contains(r.NewsId))
            .GroupBy(r => PushKey(r.SourceKey))
            .Select(group => group.OrderBy(r => r.ItemIndex).First().NewsId)
            .ToHashSet();
        if (leadNewsIds.Count == 0)
        {
            return 0;
        }

        var toMark = await _db.News
            .Where(n => leadNewsIds.Contains(n.Id) && n.Important != true)
            .ToListAsync(cancellationToken);
        if (toMark.Count == 0)
        {
            return 0;
        }

        var now = DateTime.Now;
        foreach (var news in toMark)
        {
            news.Important = true;
            news.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 按群发头条批量设为展示封面（重要）。/";
        }

        await _recorder.RecordAsync(_db, actor, $"按公众号群发头条批量设置了 {toMark.Count} 条新闻展示封面。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return toMark.Count;
    }

    /// <summary>
    /// 同一次群发的分组键：<c>mp:{biz}:{mid}:{idx}</c> → <c>mp:{biz}:{mid}</c>，<c>api:{article_id}:{idx}</c> →
    /// <c>api:{article_id}</c>；<c>url:</c> 等取不到群发信息的键各自单独成组（视为单篇群发）。
    /// </summary>
    internal static string PushKey(string sourceKey)
    {
        if (sourceKey.StartsWith("mp:", StringComparison.Ordinal) || sourceKey.StartsWith("api:", StringComparison.Ordinal))
        {
            var lastColon = sourceKey.LastIndexOf(':');
            var prefixParts = sourceKey.StartsWith("mp:", StringComparison.Ordinal) ? 4 : 3;
            if (sourceKey.Split(':').Length == prefixParts && lastColon > 0)
            {
                return sourceKey[..lastColon];
            }
        }

        return sourceKey;
    }
}
