using System.Data.Common;
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
            .Select(SzaipaHomeProjections.PublicationDetailLegacy)
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var type = 0;
        var preface = string.Empty;
        var signature = string.Empty;
        try
        {
            var template = await _context.Publication
                .AsNoTracking()
                .Where(publication => publication.Id == id)
                .Select(publication => new { publication.Type, publication.Preface, publication.Signature })
                .FirstOrDefaultAsync(cancellationToken);

            if (template is not null)
            {
                type = template.Type;
                preface = template.Preface ?? string.Empty;
                signature = template.Signature ?? string.Empty;
            }
        }
        catch (DbException exception) when (IsMissingOptionalTemplateColumn(exception))
        {
            // Type/Preface/Signature are additive fields. Before their schema migration, retain the legacy
            // gallery rather than making every historic Publication route fail.
        }

        IReadOnlyList<ExhibitionWorkModel> works = Array.Empty<ExhibitionWorkModel>();
        if (type == 1)
        {
            try
            {
                works = await _context.ExhibitionWork
                    .AsNoTracking()
                    .Where(work => work.PublicationId == id)
                    .OrderBy(work => work.SortOrder)
                    .ThenBy(work => work.Id)
                    .Select(SzaipaHomeProjections.ExhibitionWorkSummary)
                    .ToListAsync(cancellationToken);
            }
            catch (DbException exception) when (IsMissingExhibitionWorkTable(exception))
            {
                // ExhibitionWork is an additive modern feature. Older read-only databases do not have the table yet;
                // the existing gallery must remain available with an empty optional works catalogue.
            }
        }

        detail = new PublicationDetailModel
        {
            Id = detail.Id,
            TitleCn = detail.TitleCn,
            TitleEn = detail.TitleEn,
            StartDate = detail.StartDate,
            EndDate = detail.EndDate,
            FolderName = detail.FolderName,
            MaxImg = detail.MaxImg,
            CoverPath = detail.CoverPath,
            LogoPath = detail.LogoPath,
            MaxImagePath = detail.MaxImagePath,
            Location = detail.Location,
            Organizer = detail.Organizer,
            Host = detail.Host,
            CoHost = detail.CoHost,
            EditRecord = detail.EditRecord,
            Type = type,
            Preface = preface,
            Signature = signature,
            Works = works
        };

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

    private static bool IsMissingExhibitionWorkTable(DbException exception)
    {
        var message = exception.Message;
        return message.Contains("ExhibitionWork", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such table", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMissingOptionalTemplateColumn(DbException exception)
    {
        var message = exception.Message;
        return message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no such column", StringComparison.OrdinalIgnoreCase);
    }
}
