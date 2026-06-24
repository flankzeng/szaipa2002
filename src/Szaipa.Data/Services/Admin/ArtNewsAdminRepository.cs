using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class ArtNewsAdminRepository : IArtNewsAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public ArtNewsAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<ArtNewsListItem>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query =
            from a in _db.ArtNews.AsNoTracking()
            join artist in _db.Artist.AsNoTracking() on a.ArtistId equals artist.Id into joined
            from artist in joined.DefaultIfEmpty()
            orderby a.Id descending
            select new ArtNewsListItem
            {
                Id = a.Id,
                Title = a.Title,
                ArtistId = a.ArtistId,
                ArtistName = artist != null ? artist.ArtistNameCN : null,
                Date = a.Date
            };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<ArtNewsListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<ArtNews?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.ArtNews.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken) =>
        await _db.Artist.AsNoTracking()
            .OrderBy(a => a.ArtistNameCN)
            .Select(a => new ArtistOption(a.Id, a.ArtistNameCN ?? $"#{a.Id}"))
            .ToListAsync(cancellationToken);

    public async Task<int> CreateAsync(ArtNews input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var artNews = new ArtNews
        {
            ArtistId = input.ArtistId,
            Title = input.Title,
            SubTitle = input.SubTitle,
            Content = input.Content,
            CoverPath = input.CoverPath,
            Date = input.Date ?? now,
            ReadCount = 0,
            EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 编写了此艺术家动态。/"
        };

        _db.ArtNews.Add(artNews);
        await _operationRecorder.RecordAsync(_db, actor, $"编写了 {artNews.Title}的艺术家动态。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return artNews.Id;
    }

    public async Task<bool> UpdateAsync(ArtNews input, AdminActor actor, CancellationToken cancellationToken)
    {
        var artNews = await _db.ArtNews.FirstOrDefaultAsync(a => a.Id == input.Id, cancellationToken);
        if (artNews is null)
        {
            return false;
        }

        // Unlike the legacy ArtNewsEdit (which assigned every field to itself and changed nothing), actually
        // apply the edited fields.
        artNews.ArtistId = input.ArtistId;
        artNews.Title = input.Title;
        artNews.SubTitle = input.SubTitle;
        artNews.Content = input.Content;
        if (input.Date.HasValue)
        {
            artNews.Date = input.Date;
        }

        if (!string.IsNullOrEmpty(input.CoverPath))
        {
            artNews.CoverPath = input.CoverPath;
        }

        var now = DateTime.Now;
        artNews.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此艺术家动态。/";

        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {artNews.Title}的艺术家动态。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var artNews = await _db.ArtNews.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (artNews is null)
        {
            return false;
        }

        _db.ArtNews.Remove(artNews);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {artNews.Title}的艺术家动态。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
