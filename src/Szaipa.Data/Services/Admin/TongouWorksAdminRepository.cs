using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Write-side repository for the 同构(Tongou) Works module, replacing the legacy
/// <c>Project_TongouController</c> WorkAdd/WorkEdit actions. Unlike legacy (which matched the artist by
/// name string from a free-text field — fragile, case/whitespace-sensitive), the artist is selected by Id
/// from a dropdown (<see cref="GetArtistOptionsAsync"/>), same UX as the Szaipa-side artist-scoped modules.
/// Cross-database audit logging follows the same two-SaveChanges pattern as <see cref="TongouAtristAdminRepository"/>.
/// </summary>
public sealed class TongouWorksAdminRepository : ITongouWorksAdminRepository
{
    private readonly TongouAdminContext _tongouDb;
    private readonly SzaipaAdminContext _szaipaDb;
    private readonly IOperationRecorder _operationRecorder;

    public TongouWorksAdminRepository(
        TongouAdminContext tongouDb,
        SzaipaAdminContext szaipaDb,
        IOperationRecorder operationRecorder)
    {
        _tongouDb = tongouDb;
        _szaipaDb = szaipaDb;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<TongouWorkListItem>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query =
            from w in _tongouDb.TongouWorks.AsNoTracking()
            join artist in _tongouDb.TongouAtrist.AsNoTracking() on w.Atristid equals artist.id into joined
            from artist in joined.DefaultIfEmpty()
            orderby w.id descending
            select new TongouWorkListItem
            {
                Id = w.id,
                Title = w.Title,
                ArtistId = w.Atristid,
                ArtistName = artist != null ? artist.Name : w.AtristidName
            };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<TongouWorkListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<TongouWorks?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _tongouDb.TongouWorks.AsNoTracking().FirstOrDefaultAsync(w => w.id == id, cancellationToken);

    public async Task<int> CreateAsync(TongouWorks input, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = await _tongouDb.TongouAtrist.FirstOrDefaultAsync(a => a.id == input.Atristid, cancellationToken);

        var work = new TongouWorks
        {
            Atristid = input.Atristid,
            AtristidName = artist?.Name,
            Title = input.Title,
            ImgPath = input.ImgPath,
            Size = input.Size,
            Type = input.Type,
            CreationDate = input.CreationDate,
            VisityCount = 0
        };

        _tongouDb.TongouWorks.Add(work);
        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"新增了同构作品 {work.Title}。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return work.id;
    }

    public async Task<bool> UpdateAsync(TongouWorks input, AdminActor actor, CancellationToken cancellationToken)
    {
        var work = await _tongouDb.TongouWorks.FirstOrDefaultAsync(w => w.id == input.id, cancellationToken);
        if (work is null)
        {
            return false;
        }

        // Only the editable fields are copied; Atristid/AtristidName/VisityCount/HotCount stay as originally
        // stored (matches legacy WorkEdit, which never reassigns the artist on edit).
        work.Title = input.Title;
        work.Size = input.Size;
        work.Type = input.Type;
        work.CreationDate = input.CreationDate;
        if (!string.IsNullOrEmpty(input.ImgPath))
        {
            work.ImgPath = input.ImgPath;
        }

        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"修改了同构作品 {work.Title}。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var work = await _tongouDb.TongouWorks.FirstOrDefaultAsync(w => w.id == id, cancellationToken);
        if (work is null)
        {
            return false;
        }

        _tongouDb.TongouWorks.Remove(work);
        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"删除了同构作品 {work.Title}。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken) =>
        await _tongouDb.TongouAtrist.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new ArtistOption(a.id, a.Name ?? $"#{a.id}"))
            .ToListAsync(cancellationToken);
}
