using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Read-only EF Core implementation of the Artist public link. Mirrors the legacy HomeController:
/// <c>newvip</c> orders by Id descending (legacy <c>vip</c> was ascending), <c>newArt</c> loads the artist,
/// all of the artist's works (no cap), the per-feed side panels via an UNORDERED Take(1) (legacy does not
/// OrderBy — the publication slot reuses ArtNews), and the per-artist Exhibition feed. No visit-count writes.
/// </summary>
public sealed class ArtistReadRepository : IArtistReadRepository
{
    private const int SidePanelItemCount = LegacyDisplayRules.ArtistSidePanelItemCount;

    private readonly SzaipaLegacyReadContext _context;

    public ArtistReadRepository(SzaipaLegacyReadContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ArtistSummaryModel>> GetArtistsAsync(
        bool newestFirst,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Artist> query = _context.Artist.AsNoTracking();

        query = newestFirst
            ? query.OrderByDescending(artist => artist.Id)
            : query.OrderBy(artist => artist.Id);

        return await query.Select(SzaipaHomeProjections.ArtistSummary).ToListAsync(cancellationToken);
    }

    public async Task<ArtistDetailModel?> GetArtistByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Artist
            .AsNoTracking()
            .Where(artist => artist.Id == id)
            .Select(SzaipaHomeProjections.ArtistDetail)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkSummaryModel>> GetArtistWorksAsync(
        int artistId,
        CancellationToken cancellationToken = default)
    {
        return await LoadWorksAsync(artistId, cancellationToken);
    }

    public async Task<ArtistProfileSnapshotModel?> GetArtistProfileAsync(
        int artistId,
        CancellationToken cancellationToken = default)
    {
        var artist = await _context.Artist
            .AsNoTracking()
            .Where(item => item.Id == artistId)
            .Select(SzaipaHomeProjections.ArtistDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (artist is null)
        {
            return null;
        }

        var works = await LoadWorksAsync(artistId, cancellationToken);

        // Legacy newArt uses an UNORDERED Take(1) per feed (no OrderBy) — reproduce that, do not sort by Date.
        var latestNews = await _context.ArtNews
            .AsNoTracking()
            .Where(news => news.ArtistId == artistId)
            .Take(SidePanelItemCount)
            .Select(SzaipaHomeProjections.ArtNewsSidePanel)
            .ToListAsync(cancellationToken);

        // The legacy "publication" side panel reads ArtNews again (explicit compatibility placeholder).
        var latestPublications = await _context.ArtNews
            .AsNoTracking()
            .Where(news => news.ArtistId == artistId)
            .Take(SidePanelItemCount)
            .Select(SzaipaHomeProjections.ArtNewsSidePanel)
            .ToListAsync(cancellationToken);

        var latestFavorites = await _context.Fav
            .AsNoTracking()
            .Where(fav => fav.ArtistId == artistId)
            .Take(SidePanelItemCount)
            .Select(SzaipaHomeProjections.FavSidePanel)
            .ToListAsync(cancellationToken);

        var latestAuctions = await _context.Auction
            .AsNoTracking()
            .Where(auction => auction.ArtistId == artistId)
            .Take(SidePanelItemCount)
            .Select(SzaipaHomeProjections.AuctionSidePanel)
            .ToListAsync(cancellationToken);

        var relatedExhibitions = await _context.Exhibition
            .AsNoTracking()
            .Where(exhibition => exhibition.ArtistId == artistId)
            .Select(SzaipaHomeProjections.ArtistExhibition)
            .ToListAsync(cancellationToken);

        // 后台勾选并排序的关联展览紧跟在手工 Exhibition 行之后。
        relatedExhibitions.AddRange(await LoadLinkedExhibitionsAsync(artistId, cancellationToken));

        return LegacyReadModelComposer.CreateArtistProfileSnapshot(
            artist,
            works,
            latestNews,
            latestPublications,
            latestFavorites,
            latestAuctions,
            relatedExhibitions,
            await ShowDefaultExhibitionsAsync(artistId, cancellationToken));
    }

    /// <summary>
    /// 作品集顺序：先按后台维护的 <c>Sort</c>、再按 Id。未执行 T4 脚本的老库没有 Sort 列，此时回退到旧行为
    /// （不排序 = 主键顺序），前台绝不因缺列 500。
    /// </summary>
    private async Task<List<WorkSummaryModel>> LoadWorksAsync(int artistId, CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Works
                .AsNoTracking()
                .Where(work => work.ArtistId == artistId)
                .OrderBy(work => work.Sort)
                .ThenBy(work => work.Id)
                .Select(SzaipaHomeProjections.WorkSummary)
                .ToListAsync(cancellationToken);
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            return await _context.Works
                .AsNoTracking()
                .Where(work => work.ArtistId == artistId)
                .Select(SzaipaHomeProjections.WorkSummary)
                .ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 后台为艺术家勾选并排序的关联展览（<c>ArtistPublication</c> → <c>Publication</c>），转成与 Exhibition 行同形的
    /// 展讯条目。表不存在（老库）时返回空列表。
    /// </summary>
    private async Task<IReadOnlyList<ArtistExhibitionItemModel>> LoadLinkedExhibitionsAsync(
        int artistId,
        CancellationToken cancellationToken)
    {
        try
        {
            var links = await _context.ArtistPublication.AsNoTracking()
                .Where(link => link.ArtistId == artistId)
                .OrderBy(link => link.Sort)
                .ThenBy(link => link.Id)
                .Select(link => new { link.Id, link.PublicationId })
                .ToListAsync(cancellationToken);
            var publicationIds = links.Select(link => link.PublicationId).ToArray();
            var rows = await _context.Publication.AsNoTracking()
                .Where(publication => publicationIds.Contains(publication.Id))
                .Select(publication => new
                {
                    publication.Id,
                    publication.TitleCN,
                    publication.FolderName,
                    publication.CoverPath,
                    publication.Location,
                    publication.StartDate,
                    publication.EndDate
                })
                .ToListAsync(cancellationToken);
            var databaseById = rows.ToDictionary(row => row.Id);
            var resolved = new List<ArtistExhibitionItemModel>(links.Count);
            foreach (var link in links)
            {
                if (databaseById.TryGetValue(link.PublicationId, out var row))
                {
                    resolved.Add(new ArtistExhibitionItemModel
                    {
                        Id = row.Id,
                        ArtistId = artistId,
                        Title = row.TitleCN ?? string.Empty,
                        CoverUrl = string.IsNullOrWhiteSpace(row.CoverPath)
                            ? string.Empty
                            : $"/Content/images/{row.FolderName}/{row.CoverPath}",
                        Location = row.Location ?? string.Empty,
                        Link = $"/Home/Publication/{row.Id}",
                        StartDate = row.StartDate?.ToString("yyyy.MM.dd") ?? string.Empty,
                        EndDate = row.EndDate?.ToString("yyyy.MM.dd") ?? string.Empty
                    });
                    continue;
                }

                var fallback = ArtistExhibitionCompatibilityCatalog.Find(link.PublicationId);
                if (fallback is not null)
                {
                    resolved.Add(new ArtistExhibitionItemModel
                    {
                        Id = fallback.Id,
                        ArtistId = artistId,
                        Title = fallback.Title,
                        CoverUrl = fallback.CoverUrl,
                        Location = fallback.Location,
                        Link = fallback.Link,
                        StartDate = fallback.StartDate,
                        EndDate = fallback.EndDate
                    });
                }
            }

            return resolved;
        }
        catch (DbException exception) when (MissingSchema.IsMissingTable(exception, "ArtistPublication"))
        {
            return Array.Empty<ArtistExhibitionItemModel>();
        }
    }

    /// <summary>缺 <c>Artist.HideDefaultExhibitions</c> 列（老库）或艺术家不存在时一律 true = 保持旧页面。</summary>
    private async Task<bool> ShowDefaultExhibitionsAsync(int artistId, CancellationToken cancellationToken)
    {
        try
        {
            var hide = await _context.Artist
                .AsNoTracking()
                .Where(artist => artist.Id == artistId)
                .Select(artist => artist.HideDefaultExhibitions)
                .FirstOrDefaultAsync(cancellationToken);
            return !hide;
        }
        catch (DbException exception) when (MissingSchema.IsMissingColumn(exception))
        {
            return true;
        }
    }

    public async Task<ArtistArchiveSnapshotModel?> GetArtistArchiveAsync(
        int artistId,
        CancellationToken cancellationToken = default)
    {
        var artist = await GetArtistByIdAsync(artistId, cancellationToken);
        if (artist is null)
        {
            return null;
        }

        var news = await _context.ArtNews
            .AsNoTracking()
            .Where(item => item.ArtistId == artistId)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Select(SzaipaHomeProjections.ArtNewsSummary)
            .ToListAsync(cancellationToken);

        var favorites = await _context.Fav
            .AsNoTracking()
            .Where(item => item.ArtistId == artistId)
            .OrderBy(item => item.Id)
            .Select(SzaipaHomeProjections.ArtistFavorite)
            .ToListAsync(cancellationToken);

        var auctions = await _context.Auction
            .AsNoTracking()
            .Where(item => item.ArtistId == artistId)
            .OrderBy(item => item.Id)
            .Select(SzaipaHomeProjections.ArtistAuction)
            .ToListAsync(cancellationToken);

        return new ArtistArchiveSnapshotModel
        {
            Artist = artist,
            News = news,
            Favorites = favorites,
            Auctions = auctions
        };
    }

    public async Task<ArtistArticleSnapshotModel?> GetArtistArticleAsync(
        int articleId,
        CancellationToken cancellationToken = default)
    {
        var article = await _context.ArtNews
            .AsNoTracking()
            .Where(item => item.Id == articleId)
            .Select(SzaipaHomeProjections.ArtNewsSummary)
            .FirstOrDefaultAsync(cancellationToken);
        if (article is null)
        {
            return null;
        }

        var artist = await GetArtistByIdAsync(article.ArtistId, cancellationToken);
        return artist is null
            ? null
            : new ArtistArticleSnapshotModel { Artist = artist, Article = article };
    }
}
