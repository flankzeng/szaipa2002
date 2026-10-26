using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Szaipa.Data.Composition;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;
using Szaipa.Data.Services.WeChat;

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
    private readonly int? _legacyNewsMaxId;

    public NewsReadRepository(SzaipaLegacyReadContext context, IOptions<WeChatOptions>? weChatOptions = null)
    {
        _context = context;
        _legacyNewsMaxId = weChatOptions?.Value.LegacyNewsMaxId;
    }

    /// <summary>
    /// 前台列表用的新闻源：已套用"隐藏的同步文章 / 旧手录新闻"过滤。<paramref name="applyVisibility"/>=false
    /// 为降级路径（同步记录表不存在），等价于改造前的 <c>_context.News</c>。
    /// </summary>
    private IQueryable<News> NewsSource(bool applyVisibility)
    {
        var query = _context.News.AsNoTracking();
        if (!applyVisibility)
        {
            return query;
        }

        query = query.Where(news => !_context.WeChatSyncRecord.Any(record => record.NewsId == news.Id && record.IsHidden));
        if (_legacyNewsMaxId is > 0)
        {
            var legacyMax = _legacyNewsMaxId.Value;
            query = query.Where(news => news.Id > legacyMax
                || _context.WeChatSyncRecord.Any(record => record.NewsId == news.Id));
        }

        return query;
    }

    private async Task<T> WithVisibilityAsync<T>(Func<IQueryable<News>, Task<T>> run)
    {
        try
        {
            return await run(NewsSource(applyVisibility: true));
        }
        catch (DbException exception) when (WeChatSchema.IsMissingSyncTable(exception))
        {
            // 加性新表尚未建立：前台照旧显示全部 News，绝不 500。
            return await run(NewsSource(applyVisibility: false));
        }
    }

    public async Task<HomePageSnapshotModel> GetHomePageSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var latestNews = await WithVisibilityAsync(source => source
            .OrderByDescending(news => news.Date)
            .Take(LegacyDisplayRules.LandingPageNewsCount)
            .Select(SzaipaHomeProjections.NewsSummary)
            .ToListAsync(cancellationToken));

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
        return await WithVisibilityAsync(source => QueryLatestNews(source, count).ToListAsync(cancellationToken));
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
        return await WithVisibilityAsync(source => QueryLatestNews(source, count).ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<NewsSummaryModel>> SearchNewsAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        keyword ??= string.Empty;

        return await WithVisibilityAsync(source => source
            .Where(news =>
                (news.Title != null && news.Title.Contains(keyword)) ||
                (news.Content != null && news.Content.Contains(keyword)))
            .OrderByDescending(news => news.Date)
            .Select(SzaipaHomeProjections.NewsSummary)
            .ToListAsync(cancellationToken));
    }

    private static IQueryable<NewsSummaryModel> QueryLatestNews(IQueryable<News> source, int count)
    {
        IQueryable<News> query = source.OrderByDescending(news => news.Date);

        if (count > 0)
        {
            query = query.Take(count);
        }

        return query.Select(SzaipaHomeProjections.NewsSummary);
    }
}
