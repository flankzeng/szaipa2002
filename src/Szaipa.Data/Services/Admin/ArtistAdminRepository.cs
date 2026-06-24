using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class ArtistAdminRepository : IArtistAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public ArtistAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<PagedResult<Artist>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Artist.AsNoTracking().OrderByDescending(a => a.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Artist>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<Artist?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.Artist.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<int> CreateAsync(Artist input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var artist = new Artist
        {
            ArtistNameCN = input.ArtistNameCN,
            ArtistNameEN = input.ArtistNameEN,
            Sex = input.Sex,
            Nation = input.Nation,
            City = input.City,
            Title = input.Title,
            Position = input.Position,
            Color1 = input.Color1,
            Color2 = input.Color2,
            Path = input.Path,
            Path1 = input.Path1,
            Path2 = input.Path2,
            Introduction = input.Introduction,
            Honor = input.Honor,
            DeedsThings = input.DeedsThings,
            WorkCount = 0,
            VisitCount = 0,
            AddDate = now,
            EndDate = now,
            EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 创建了此会员的条目。/"
        };

        _db.Artist.Add(artist);
        await _operationRecorder.RecordAsync(_db, actor, $"创建了 {artist.ArtistNameCN}的会员条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return artist.Id;
    }

    public async Task<bool> UpdateAsync(Artist input, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = await _db.Artist.FirstOrDefaultAsync(a => a.Id == input.Id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        // Only the editable fields are copied; WorkCount/VisitCount/AddDate stay as originally stored.
        artist.ArtistNameCN = input.ArtistNameCN;
        artist.ArtistNameEN = input.ArtistNameEN;
        artist.Sex = input.Sex;
        artist.Nation = input.Nation;
        artist.City = input.City;
        artist.Title = input.Title;
        artist.Position = input.Position;
        artist.Color1 = input.Color1;
        artist.Color2 = input.Color2;
        artist.Introduction = input.Introduction;
        artist.Honor = input.Honor;
        artist.DeedsThings = input.DeedsThings;
        if (!string.IsNullOrEmpty(input.Path))
        {
            artist.Path = input.Path;
        }

        if (!string.IsNullOrEmpty(input.Path1))
        {
            artist.Path1 = input.Path1;
        }

        if (!string.IsNullOrEmpty(input.Path2))
        {
            artist.Path2 = input.Path2;
        }

        var now = DateTime.Now;
        artist.EndDate = now;
        artist.EditRecord += $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 修改了此会员的条目。/";

        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {artist.ArtistNameCN}的会员条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken)
    {
        var artist = await _db.Artist.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        _db.Artist.Remove(artist);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {artist.ArtistNameCN}的会员条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
