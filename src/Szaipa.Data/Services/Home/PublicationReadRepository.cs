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
        if (type == PublicationTypes.Important || type == PublicationTypes.Topic)
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

        var themeColor = string.Empty;
        var bannerPath = string.Empty;
        IReadOnlyList<PublicationBlockModel> blocks = Array.Empty<PublicationBlockModel>();
        if (type == PublicationTypes.Topic)
        {
            (themeColor, bannerPath) = await LoadTopicThemeAsync(id, cancellationToken);
            blocks = await LoadTopicBlocksAsync(id, cancellationToken);
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
            Status = detail.Status,
            Type = type,
            Preface = preface,
            Signature = signature,
            Works = works,
            ThemeColor = themeColor,
            BannerPath = bannerPath,
            Blocks = blocks
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

    public async Task<IReadOnlyList<FeaturedExhibitionModel>> GetHomeFeaturedAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // FeaturedExhibition is an additive table (2026-10-26 script). Without it the home page simply keeps
            // its original hard-coded exhibition blocks. Hidden rows are returned too: the view needs their links
            // to know which hard-coded blocks have been taken over (and must stay hidden).
            var rows = await _context.FeaturedExhibition
                .AsNoTracking()
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new FeaturedExhibitionModel
                {
                    Id = row.Id,
                    SortOrder = row.SortOrder,
                    IsVisible = row.IsVisible,
                    ShowInCarousel = row.ShowInCarousel,
                    Title = row.Title ?? string.Empty,
                    YearText = row.YearText ?? string.Empty,
                    LinkUrl = row.LinkUrl ?? string.Empty,
                    InfoLines = FeaturedExhibitionContent.SplitLines(row.InfoLines),
                    ImageUrls = FeaturedExhibitionContent.SplitPaths(row.ImageUrls),
                    LogoUrl = FeaturedExhibitionContent.SafePathOrEmpty(row.LogoUrl),
                    CoverUrl = FeaturedExhibitionContent.SafePathOrEmpty(row.CoverUrl),
                    StartDate = row.StartDate,
                    EndDate = row.EndDate,
                    Status = row.Status
                })
                .Where(model => FeaturedExhibitionContent.IsSafeLink(model.LinkUrl))
                .ToList();
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingTable(exception, "FeaturedExhibition")
            || SchemaCompatibility.IsMissingColumn(exception))
        {
            return Array.Empty<FeaturedExhibitionModel>();
        }
    }

    private async Task<(string ThemeColor, string BannerPath)> LoadTopicThemeAsync(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var theme = await _context.Publication
                .AsNoTracking()
                .Where(publication => publication.Id == id)
                .Select(publication => new { publication.ThemeColor, publication.BannerPath })
                .FirstOrDefaultAsync(cancellationToken);
            return (theme?.ThemeColor ?? string.Empty, theme?.BannerPath ?? string.Empty);
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingColumn(exception))
        {
            // Theme columns not migrated yet: the topic still renders with the default accent colour.
            return (string.Empty, string.Empty);
        }
    }

    private async Task<IReadOnlyList<PublicationBlockModel>> LoadTopicBlocksAsync(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _context.PublicationBlock
                .AsNoTracking()
                .Where(block => block.PublicationId == id)
                .OrderBy(block => block.SortOrder)
                .ThenBy(block => block.Id)
                .Select(block => new PublicationBlockModel
                {
                    Id = block.Id,
                    BlockType = block.BlockType,
                    Content = block.Content
                })
                .ToListAsync(cancellationToken);
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingTable(exception, "PublicationBlock")
            || SchemaCompatibility.IsMissingColumn(exception))
        {
            // PublicationBlock is additive. Without it a 专题 simply has no blocks yet (page shows its header only).
            return Array.Empty<PublicationBlockModel>();
        }
    }

    private static bool IsMissingExhibitionWorkTable(DbException exception) =>
        SchemaCompatibility.IsMissingTable(exception, "ExhibitionWork");

    private static bool IsMissingOptionalTemplateColumn(DbException exception) =>
        SchemaCompatibility.IsMissingColumn(exception);
}
