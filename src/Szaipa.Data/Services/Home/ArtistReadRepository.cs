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
        return await _context.Works
            .AsNoTracking()
            .Where(work => work.ArtistId == artistId)
            .Select(SzaipaHomeProjections.WorkSummary)
            .ToListAsync(cancellationToken);
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

        var works = await _context.Works
            .AsNoTracking()
            .Where(work => work.ArtistId == artistId)
            .Select(SzaipaHomeProjections.WorkSummary)
            .ToListAsync(cancellationToken);

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

        return LegacyReadModelComposer.CreateArtistProfileSnapshot(
            artist,
            works,
            latestNews,
            latestPublications,
            latestFavorites,
            latestAuctions,
            relatedExhibitions);
    }
}
