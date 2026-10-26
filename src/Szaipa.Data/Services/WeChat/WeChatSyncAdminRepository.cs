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

/// <summary>公众号同步记录的后台读写（列表 / 隐藏 / 置顶）。导入本身见 <see cref="IWeChatSyncService"/>。</summary>
public interface IWeChatSyncAdminRepository
{
    Task<WeChatSyncListResult> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>隐藏/取消隐藏：隐藏后文章不再出现在前台列表、首页与侧栏推荐，详情 URL 仍可访问。</summary>
    Task<bool> SetHiddenAsync(int recordId, bool hidden, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>置顶 = 把对应 News.Important 置为 true（前台渲染为带封面的"重要新闻"大卡片）。</summary>
    Task<bool> SetPinnedAsync(int recordId, bool pinned, AdminActor actor, CancellationToken cancellationToken);
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
        news.EditRecord += $"{actor.StaffName} 于 {DateTime.Now:yyyy年MM月dd日 HH:mm:ss} {(pinned ? "置顶（标为重要）" : "取消置顶")}了此新闻条目。/";
        await _recorder.RecordAsync(_db, actor, $"{(pinned ? "置顶" : "取消置顶")}了公众号同步新闻 {news.Title}。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
