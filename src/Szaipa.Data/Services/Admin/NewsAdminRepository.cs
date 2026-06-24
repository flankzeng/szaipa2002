using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class NewsAdminRepository : INewsAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public NewsAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<News>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.News.AsNoTracking().OrderByDescending(n => n.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<News>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<News?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.News.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<int> CreateAsync(News input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var news = new News
        {
            Title = input.Title,
            Subtitle = input.Subtitle,
            Content = input.Content,
            CoverPath = input.CoverPath,
            link = input.link,
            original = input.original,
            Important = input.Important,
            Autor = actor.StaffName,
            Date = now,
            ReadCount = 0,
            Year = now.Year,
            EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 编写了此新闻条目。/"
        };

        _db.News.Add(news);
        await _operationRecorder.RecordAsync(_db, actor, $"编写了 {news.Title}的新闻条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return news.Id;
    }

    public async Task<bool> UpdateAsync(News input, AdminActor actor, CancellationToken cancellationToken)
    {
        var news = await _db.News.FirstOrDefaultAsync(n => n.Id == input.Id, cancellationToken);
        if (news is null)
        {
            return false;
        }

        // Only the editable fields are copied; Date/Autor/ReadCount/Year stay as originally stored.
        news.Title = input.Title;
        news.Subtitle = input.Subtitle;
        news.Content = input.Content;
        news.link = input.link;
        news.original = input.original;
        news.Important = input.Important;
        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            news.CoverPath = input.CoverPath;
        }

        var now = DateTime.Now;
        news.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此新闻条目。/";

        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {news.Title}的新闻条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var news = await _db.News.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (news is null)
        {
            return false;
        }

        _db.News.Remove(news);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {news.Title}的新闻条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
