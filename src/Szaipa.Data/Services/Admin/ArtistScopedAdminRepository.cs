using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Generic write-side repository for the artist-scoped child modules (Fav/Auction/Exhibition/Works). Handles
/// the common list (joined to the artist name), lookup, create/update/delete, edit-record stamping and
/// operation logging in one transaction. Derived classes supply only the DbSet, a display noun, and how to
/// copy the editable fields (which is also where cover-preserve-on-empty lives).
/// </summary>
public abstract class ArtistScopedAdminRepository<T>
    where T : class, IArtistScopedRecord, new()
{
    protected ArtistScopedAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        Db = db;
        OperationRecorder = operationRecorder;
    }

    protected SzaipaAdminContext Db { get; }

    protected IOperationRecorder OperationRecorder { get; }

    protected abstract DbSet<T> Set { get; }

    /// <summary>Display noun for audit/log messages, e.g. "收藏"/"拍卖"/"展览".</summary>
    protected abstract string Noun { get; }

    /// <summary>
    /// Copies the editable fields from <paramref name="input"/> onto <paramref name="target"/>. Called for both
    /// create (fresh target) and update (tracked target), so cover-preserve-on-empty belongs here.
    /// </summary>
    protected abstract void ApplyEditableFields(T target, T input);

    public async Task<PagedResult<ArtistScopedListItem>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query =
            from e in Set.AsNoTracking()
            join artist in Db.Artist.AsNoTracking() on e.ArtistId equals artist.Id into joined
            from artist in joined.DefaultIfEmpty()
            orderby e.Id descending
            select new ArtistScopedListItem
            {
                Id = e.Id,
                Title = e.Title,
                ArtistId = e.ArtistId,
                ArtistName = artist != null ? artist.ArtistNameCN : null
            };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<ArtistScopedListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Set.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken) =>
        await Db.Artist.AsNoTracking()
            .OrderBy(a => a.ArtistNameCN)
            .Select(a => new ArtistOption(a.Id, a.ArtistNameCN ?? $"#{a.Id}"))
            .ToListAsync(cancellationToken);

    public async Task<int> CreateAsync(T input, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = new T();
        ApplyEditableFields(entity, input);

        var now = DateTime.Now;
        entity.EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 新增了此{Noun}。/";

        Set.Add(entity);
        await OperationRecorder.RecordAsync(Db, actor, $"新增了 {entity.Title}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(T input, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == input.Id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        ApplyEditableFields(entity, input);

        var now = DateTime.Now;
        entity.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此{Noun}。/";

        await OperationRecorder.RecordAsync(Db, actor, $"修改了 {entity.Title}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        Set.Remove(entity);
        await OperationRecorder.RecordAsync(Db, actor, $"删除了 {entity.Title}的{Noun}。", cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
