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

        var query = LegacyArtistProjection(_db.Artist.AsNoTracking()).OrderByDescending(a => a.Id);
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
        LegacyArtistProjection(_db.Artist.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    // The artist list/edit form does not need the T4 flag; omitting it keeps old admin databases readable.
    private static IQueryable<Artist> LegacyArtistProjection(IQueryable<Artist> query) => query.Select(a => new Artist
    {
        Id = a.Id,
        ArtistNameCN = a.ArtistNameCN,
        ArtistNameEN = a.ArtistNameEN,
        Sex = a.Sex,
        Path = a.Path,
        UserContent = a.UserContent,
        Nation = a.Nation,
        City = a.City,
        Title = a.Title,
        WorkCount = a.WorkCount,
        EditRecord = a.EditRecord,
        Deeds = a.Deeds,
        Honor = a.Honor,
        AddDate = a.AddDate,
        EndDate = a.EndDate,
        FlieInf = a.FlieInf,
        VisitCount = a.VisitCount,
        Activity = a.Activity,
        Color1 = a.Color1,
        Color2 = a.Color2,
        Introduction = a.Introduction,
        Position = a.Position,
        Path1 = a.Path1,
        Path2 = a.Path2,
        DeedsThings = a.DeedsThings,
        DeedsYears = a.DeedsYears
    });

    public async Task<int> CreateAsync(Artist input, AdminActor actor, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var artist = new Artist
        {
            ArtistNameCN = input.ArtistNameCN,
            ArtistNameEN = input.ArtistNameEN,
            Sex = input.Sex ?? string.Empty, // 生产库 Sex 列 NOT NULL（无默认值），未选时写空串
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
            // 新建艺术家的官网展讯完全由数据驱动：不追加旧版写死的 3 张默认活动图。
            HideDefaultExhibitions = input.HideDefaultExhibitions,
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
        var artist = _db.Artist.Local.FirstOrDefault(a => a.Id == input.Id)
            ?? await LegacyArtistProjection(_db.Artist.AsNoTracking())
                .FirstOrDefaultAsync(a => a.Id == input.Id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        if (_db.Entry(artist).State == EntityState.Detached)
        {
            _db.Artist.Attach(artist);
        }

        // Only the editable fields are copied; WorkCount/VisitCount/AddDate stay as originally stored.
        artist.ArtistNameCN = input.ArtistNameCN;
        artist.ArtistNameEN = input.ArtistNameEN;
        artist.Sex = input.Sex ?? string.Empty;
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
        var artist = _db.Artist.Local.FirstOrDefault(a => a.Id == id)
            ?? await LegacyArtistProjection(_db.Artist.AsNoTracking())
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        if (_db.Entry(artist).State == EntityState.Detached)
        {
            _db.Artist.Attach(artist);
        }
        _db.Artist.Remove(artist);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {artist.ArtistNameCN}的会员条目。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
