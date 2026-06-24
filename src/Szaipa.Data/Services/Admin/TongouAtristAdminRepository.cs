using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;

namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Write-side repository for the 同构(Tongou) Atrist module, replacing the legacy
/// <c>Project_TongouController</c> AtristAdd/AtristEdit actions. Tongou is a physically separate database
/// from Szaipa (own <see cref="TongouAdminContext"/>), but the staff audit trail (Diary/Staff) lives only in
/// the Szaipa database — so this repository writes Tongou data through <see cref="TongouAdminContext"/> and
/// records the operation through <see cref="IOperationRecorder"/> against <see cref="SzaipaAdminContext"/> in
/// a second, separate SaveChanges (the two databases cannot share one transaction).
/// </summary>
public sealed class TongouAtristAdminRepository : ITongouAtristAdminRepository
{
    private readonly TongouAdminContext _tongouDb;
    private readonly SzaipaAdminContext _szaipaDb;
    private readonly IOperationRecorder _operationRecorder;

    public TongouAtristAdminRepository(
        TongouAdminContext tongouDb,
        SzaipaAdminContext szaipaDb,
        IOperationRecorder operationRecorder)
    {
        _tongouDb = tongouDb;
        _szaipaDb = szaipaDb;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<TongouAtrist>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _tongouDb.TongouAtrist.AsNoTracking().OrderByDescending(a => a.id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TongouAtrist>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<TongouAtrist?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _tongouDb.TongouAtrist.AsNoTracking().FirstOrDefaultAsync(a => a.id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        _tongouDb.TongouAtrist.AsNoTracking().AnyAsync(a => a.Name == name, cancellationToken);

    public async Task<int> CreateAsync(TongouAtrist input, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = new TongouAtrist
        {
            Name = input.Name,
            Title = input.Title,
            HeardPath = input.HeardPath,
            AboutText = input.AboutText
        };

        _tongouDb.TongouAtrist.Add(artist);
        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"创建了同构艺术家 {artist.Name}的条目。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return artist.id;
    }

    public async Task<bool> UpdateAsync(TongouAtrist input, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = await _tongouDb.TongouAtrist.FirstOrDefaultAsync(a => a.id == input.id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        // Only the editable fields are copied; WorksCount/Aboutid/HotCount stay as originally stored
        // (matches legacy AtristEdit, which never touches them).
        artist.Name = input.Name;
        artist.Title = input.Title;
        artist.HeardPath = input.HeardPath;
        artist.AboutText = input.AboutText;
        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"修改了同构艺术家 {artist.Name}的条目。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = await _tongouDb.TongouAtrist.FirstOrDefaultAsync(a => a.id == id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        _tongouDb.TongouAtrist.Remove(artist);
        await _tongouDb.SaveChangesAsync(cancellationToken);

        await _operationRecorder.RecordAsync(_szaipaDb, actor, $"删除了同构艺术家 {artist.Name}的条目。", cancellationToken);
        await _szaipaDb.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken) =>
        await _tongouDb.TongouAtrist.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new ArtistOption(a.id, a.Name ?? $"#{a.id}"))
            .ToListAsync(cancellationToken);
}
