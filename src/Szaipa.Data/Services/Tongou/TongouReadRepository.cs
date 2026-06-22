using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contracts.Tongou;
using Szaipa.Data.Models.Tongou;

namespace Szaipa.Data.Services.Tongou;

/// <summary>
/// Read-only EF Core implementation of the Tongou public link. Mirrors the legacy Project_Tongou controller:
/// the generic artist page loads a single artist, the work-list page loads the artist plus its works filtered
/// by <c>Atristid</c>, and work detail loads one row by id. No VisityCount/HotCount writes and no SaveChanges.
/// Special id-based redirects stay in the controller layer, not here.
/// </summary>
public sealed class TongouReadRepository : ITongouReadRepository
{
    private readonly TongouLegacyReadContext _context;

    public TongouReadRepository(TongouLegacyReadContext context)
    {
        _context = context;
    }

    public async Task<TongouArtistModel?> GetArtistByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.TongouAtrist
            .AsNoTracking()
            .Where(artist => artist.id == id)
            .Select(TongouReadProjections.Artist)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TongouWorkModel?> GetWorkByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.TongouWorks
            .AsNoTracking()
            .Where(work => work.id == id)
            .Select(TongouReadProjections.Work)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TongouWorkModel>> GetWorksByArtistIdAsync(
        int artistId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TongouWorks
            .AsNoTracking()
            .Where(work => work.Atristid == artistId)
            .Select(TongouReadProjections.Work)
            .ToListAsync(cancellationToken);
    }

    public async Task<TongouArtistProfileSnapshotModel?> GetArtistProfileAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var artist = await _context.TongouAtrist
            .AsNoTracking()
            .Where(item => item.id == id)
            .Select(TongouReadProjections.Artist)
            .FirstOrDefaultAsync(cancellationToken);

        if (artist is null)
        {
            return null;
        }

        var works = await _context.TongouWorks
            .AsNoTracking()
            .Where(work => work.Atristid == id)
            .Select(TongouReadProjections.Work)
            .ToListAsync(cancellationToken);

        return LegacyReadModelComposer.CreateTongouArtistProfileSnapshot(artist, works);
    }
}
