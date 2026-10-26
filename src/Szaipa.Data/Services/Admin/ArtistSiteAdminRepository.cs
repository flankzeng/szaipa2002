using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;

namespace Szaipa.Data.Services.Admin;

/// <inheritdoc />
public sealed class ArtistSiteAdminRepository : IArtistSiteAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public ArtistSiteAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<bool> SupportsWorksSortAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.Works.AsNoTracking().Select(work => work.Sort).Take(1).ToListAsync(cancellationToken);
            return true;
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            return false;
        }
    }

    public async Task<ArtistSiteOverview?> GetOverviewAsync(int artistId, CancellationToken cancellationToken)
    {
        var artist = await _db.Artist.AsNoTracking()
            .Where(a => a.Id == artistId)
            .Select(a => new Artist
            {
                Id = a.Id,
                ArtistNameCN = a.ArtistNameCN,
                ArtistNameEN = a.ArtistNameEN,
                Color1 = a.Color1,
                Color2 = a.Color2,
                Path = a.Path,
                Path1 = a.Path1,
                Path2 = a.Path2,
                Introduction = a.Introduction,
                Honor = a.Honor,
                Position = a.Position
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (artist is null)
        {
            return null;
        }

        var linkedCount = 0;
        var linksSupported = true;
        try
        {
            linkedCount = await _db.ArtistPublication.CountAsync(l => l.ArtistId == artistId, cancellationToken);
        }
        catch (DbException exception) when (MissingSchema.IsMissingTable(exception, "ArtistPublication"))
        {
            linksSupported = false;
        }

        var sortSupported = true;
        try
        {
            // 仅探测列是否存在，不取数据。
            await _db.Works.AsNoTracking().Where(w => w.ArtistId == artistId).Select(w => w.Sort).FirstOrDefaultAsync(cancellationToken);
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            sortSupported = false;
        }

        var defaultExhibitionFlagSupported = true;
        try
        {
            await _db.Artist.AsNoTracking()
                .Where(a => a.Id == artistId)
                .Select(a => a.HideDefaultExhibitions)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            defaultExhibitionFlagSupported = false;
        }

        return new ArtistSiteOverview
        {
            Artist = artist,
            WorkCount = await _db.Works.CountAsync(w => w.ArtistId == artistId, cancellationToken),
            NewsCount = await _db.ArtNews.CountAsync(n => n.ArtistId == artistId, cancellationToken),
            FavCount = await _db.Fav.CountAsync(f => f.ArtistId == artistId, cancellationToken),
            AuctionCount = await _db.Auction.CountAsync(a => a.ArtistId == artistId, cancellationToken),
            ExhibitionRowCount = await _db.Exhibition.CountAsync(e => e.ArtistId == artistId, cancellationToken),
            LinkedPublicationCount = linkedCount,
            WorksSortSupported = sortSupported,
            ExhibitionLinksSupported = linksSupported,
            DefaultExhibitionFlagSupported = defaultExhibitionFlagSupported
        };
    }

    public async Task<ArtistWorkList> GetWorksAsync(int artistId, CancellationToken cancellationToken)
    {
        try
        {
            var items = await _db.Works.AsNoTracking()
                .Where(w => w.ArtistId == artistId)
                .OrderBy(w => w.Sort)
                .ThenBy(w => w.Id)
                .Select(w => new ArtistWorkItem
                {
                    Id = w.Id,
                    Title = w.Title,
                    Content = w.Content,
                    Path = w.Path,
                    Sort = w.Sort
                })
                .ToListAsync(cancellationToken);
            return new ArtistWorkList { Items = items };
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            var items = await _db.Works.AsNoTracking()
                .Where(w => w.ArtistId == artistId)
                .OrderBy(w => w.Id)
                .Select(w => new ArtistWorkItem { Id = w.Id, Title = w.Title, Content = w.Content, Path = w.Path })
                .ToListAsync(cancellationToken);
            return new ArtistWorkList { Items = items, SortSupported = false };
        }
    }

    public async Task<IReadOnlyList<int>> AddWorksAsync(
        int artistId,
        IReadOnlyList<NewArtistWork> works,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        if (works.Count == 0)
        {
            return Array.Empty<int>();
        }

        var artistName = await _db.Artist.AsNoTracking()
            .Where(a => a.Id == artistId)
            .Select(a => a.ArtistNameCN)
            .FirstOrDefaultAsync(cancellationToken);
        var max = await _db.Works.Where(w => w.ArtistId == artistId).MaxAsync(w => (int?)w.Sort, cancellationToken) ?? 0;

        var now = DateTime.Now;
        var created = new List<Works>(works.Count);
        foreach (var input in works)
        {
            max++;
            var entity = new Works
            {
                ArtistId = artistId,
                Path = input.Path,
                Title = string.IsNullOrWhiteSpace(input.Title) ? "未命名" : input.Title.Trim(),
                Content = input.Content?.Trim(),
                Sort = max,
                EditRecord = $"{actor.StaffName} 于 {now:yyyy年MM月dd日 HH:mm:ss} 新增了此作品。/"
            };
            _db.Works.Add(entity);
            created.Add(entity);
        }

        await _operationRecorder.RecordAsync(
            _db, actor, $"批量新增了 {artistName} 的 {works.Count} 件作品。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return created.Select(w => w.Id).ToArray();
    }

    public async Task<bool> UpdateWorkAsync(
        int artistId,
        int workId,
        string? title,
        string? content,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == workId && w.ArtistId == artistId, cancellationToken);
        if (work is null)
        {
            return false;
        }

        work.Title = title?.Trim();
        work.Content = content?.Trim();
        work.EditRecord += $"{actor.StaffName} 于 {DateTime.Now:yyyy年MM月dd日 HH:mm:ss} 修改了此作品。/";
        await _operationRecorder.RecordAsync(_db, actor, $"修改了 {work.Title}的作品。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteWorkAsync(
        int artistId,
        int workId,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == workId && w.ArtistId == artistId, cancellationToken);
        if (work is null)
        {
            return false;
        }

        _db.Works.Remove(work);
        await _operationRecorder.RecordAsync(_db, actor, $"删除了 {work.Title}的作品。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderWorksAsync(
        int artistId,
        IReadOnlyList<int> orderedWorkIds,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var works = await _db.Works.Where(w => w.ArtistId == artistId).ToListAsync(cancellationToken);
        var byId = works.ToDictionary(w => w.Id);

        // 先排请求里给出的（忽略不属于该艺术家/重复的 Id），再把没出现的按原有顺序接在后面。
        var ordered = new List<Works>(works.Count);
        var seen = new HashSet<int>();
        foreach (var id in orderedWorkIds)
        {
            if (byId.TryGetValue(id, out var work) && seen.Add(id))
            {
                ordered.Add(work);
            }
        }

        ordered.AddRange(works.Where(w => !seen.Contains(w.Id)).OrderBy(w => w.Sort).ThenBy(w => w.Id));

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Sort = index + 1;
        }

        await _operationRecorder.RecordAsync(_db, actor, $"调整了艺术家 #{artistId} 的作品顺序。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicationOption>> GetPublicationOptionsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Publication.AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Select(p => new PublicationOption(p.Id, p.TitleCN ?? $"#{p.Id}", p.StartDate, p.Location))
            .ToListAsync(cancellationToken);
        return rows.Concat(Szaipa.Data.Services.Home.ArtistExhibitionCompatibilityCatalog.GetOptions())
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .ToArray();
    }

    public async Task<ArtistExhibitionLinks> GetExhibitionLinksAsync(int artistId, CancellationToken cancellationToken)
    {
        try
        {
            var links = await _db.ArtistPublication.AsNoTracking()
                .Where(link => link.ArtistId == artistId)
                .OrderBy(link => link.Sort)
                .ThenBy(link => link.Id)
                .Select(link => link.PublicationId)
                .ToListAsync(cancellationToken);
            var options = await GetPublicationOptionsAsync(cancellationToken);
            var optionsById = options.ToDictionary(option => option.Id);
            var linked = links.Where(optionsById.ContainsKey).Select(id => optionsById[id]).ToArray();

            var hide = await _db.Artist.AsNoTracking()
                .Where(a => a.Id == artistId)
                .Select(a => a.HideDefaultExhibitions)
                .FirstOrDefaultAsync(cancellationToken);
            return new ArtistExhibitionLinks { Linked = linked, HideDefaultExhibitions = hide };
        }
        catch (DbException exception) when (MissingSchema.IsMissingTable(exception, "ArtistPublication")
                                           || MissingSchema.IsMissingColumn(exception))
        {
            return new ArtistExhibitionLinks { Linked = Array.Empty<PublicationOption>(), Supported = false };
        }
    }

    public async Task<bool> SaveExhibitionLinksAsync(
        int artistId,
        IReadOnlyList<int> orderedPublicationIds,
        bool hideDefaultExhibitions,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var artist = await _db.Artist.FirstOrDefaultAsync(a => a.Id == artistId, cancellationToken);
        if (artist is null)
        {
            return false;
        }

        var requested = orderedPublicationIds.Distinct().ToList();
        var existingIds = await _db.Publication.AsNoTracking()
            .Where(p => requested.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var compatibleIds = Szaipa.Data.Services.Home.ArtistExhibitionCompatibilityCatalog.CompatibilityIds;
        var valid = requested.Where(id => existingIds.Contains(id) || compatibleIds.Contains(id)).ToList();

        _db.ArtistPublication.RemoveRange(
            await _db.ArtistPublication.Where(l => l.ArtistId == artistId).ToListAsync(cancellationToken));
        for (var index = 0; index < valid.Count; index++)
        {
            _db.ArtistPublication.Add(new ArtistPublication
            {
                ArtistId = artistId,
                PublicationId = valid[index],
                Sort = index
            });
        }

        artist.HideDefaultExhibitions = hideDefaultExhibitions;
        await _operationRecorder.RecordAsync(
            _db, actor, $"调整了 {artist.ArtistNameCN} 官网的展讯（关联 {valid.Count} 个展览）。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
