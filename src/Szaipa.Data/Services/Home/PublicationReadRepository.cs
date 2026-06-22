using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Read-only EF Core implementation of the Publication public link. Mirrors the legacy HomeController:
/// the list page orders by Id descending (legacy <c>PublicationList</c>), the detail page loads one row by
/// Id plus a neighboring list, and no ReadCount increment or SaveChanges ever runs.
/// </summary>
public sealed class PublicationReadRepository : IPublicationReadRepository
{
    private readonly SzaipaLegacyReadContext _context;

    public PublicationReadRepository(SzaipaLegacyReadContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PublicationSummaryModel>> GetLatestPublicationsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Publication> query = _context.Publication
            .AsNoTracking()
            .OrderByDescending(publication => publication.Id);

        if (count > 0)
        {
            query = query.Take(count);
        }

        return await query.Select(SzaipaHomeProjections.PublicationSummary).ToListAsync(cancellationToken);
    }

    public async Task<PublicationSummaryModel?> GetPublicationByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Publication
            .AsNoTracking()
            .Where(publication => publication.Id == id)
            .Select(SzaipaHomeProjections.PublicationSummary)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PublicationDetailSnapshotModel?> GetPublicationDetailSnapshotAsync(
        int id,
        int relatedCount,
        CancellationToken cancellationToken = default)
    {
        var detail = await _context.Publication
            .AsNoTracking()
            .Where(publication => publication.Id == id)
            .Select(SzaipaHomeProjections.PublicationDetail)
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        IQueryable<Publication> relatedQuery = _context.Publication
            .AsNoTracking()
            .OrderByDescending(publication => publication.Id);

        if (relatedCount > 0)
        {
            relatedQuery = relatedQuery.Take(relatedCount);
        }

        var related = await relatedQuery
            .Select(SzaipaHomeProjections.PublicationSummary)
            .ToListAsync(cancellationToken);

        return LegacyReadModelComposer.CreatePublicationDetailSnapshot(detail, related);
    }
}
