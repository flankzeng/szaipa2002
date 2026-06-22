using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// Read-only EF Core implementation of the News public link. All queries use no-tracking and never write.
/// Behavior mirrors the legacy <c>HomeController</c> exactly:
/// <list type="bullet">
///   <item>landing snapshot = latest <see cref="LegacyDisplayRules.LandingPageNewsCount"/> by Date desc, subtitle trimmed by the composer;</item>
///   <item>list/related = latest N by Date desc;</item>
///   <item>Important = (legacy treats null/true as Important, only explicit false is not important);</item>
///   <item>no ReadCount / Diary increments and no SaveChanges.</item>
/// </list>
/// </summary>
public sealed class NewsReadRepository : INewsReadRepository
{
    private readonly SzaipaLegacyReadContext _context;

    public NewsReadRepository(SzaipaLegacyReadContext context)
    {
        _context = context;
    }

    public async Task<HomePageSnapshotModel> GetHomePageSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var latestNews = await _context.News
            .AsNoTracking()
            .OrderByDescending(news => news.Date)
            .Take(LegacyDisplayRules.LandingPageNewsCount)
            .Select(SzaipaHomeProjections.NewsSummary)
            .ToListAsync(cancellationToken);

        var featuredPublications = await _context.Publication
            .AsNoTracking()
            .OrderByDescending(publication => publication.StartDate)
            .Select(SzaipaHomeProjections.PublicationSummary)
            .ToListAsync(cancellationToken);

        // The composer applies the legacy landing-page 90-char subtitle trim to the news rows.
        return LegacyReadModelComposer.CreateHomePageSnapshot(latestNews, featuredPublications);
    }

    public async Task<IReadOnlyList<NewsSummaryModel>> GetLatestNewsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await QueryLatestNews(count).ToListAsync(cancellationToken);
    }

    public async Task<NewsDetailModel?> GetNewsByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.News
            .AsNoTracking()
            .Where(news => news.Id == id)
            .Select(SzaipaHomeProjections.NewsDetail)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NewsSummaryModel>> GetRelatedNewsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await QueryLatestNews(count).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NewsSummaryModel>> SearchNewsAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        keyword ??= string.Empty;

        return await _context.News
            .AsNoTracking()
            .Where(news =>
                (news.Title != null && news.Title.Contains(keyword)) ||
                (news.Content != null && news.Content.Contains(keyword)))
            .OrderByDescending(news => news.Date)
            .Select(SzaipaHomeProjections.NewsSummary)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<NewsSummaryModel> QueryLatestNews(int count)
    {
        IQueryable<News> query = _context.News
            .AsNoTracking()
            .OrderByDescending(news => news.Date);

        if (count > 0)
        {
            query = query.Take(count);
        }

        return query.Select(SzaipaHomeProjections.NewsSummary);
    }
}
