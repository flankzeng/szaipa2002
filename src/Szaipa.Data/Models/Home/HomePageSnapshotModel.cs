namespace Szaipa.Data.Models.Home;

public sealed class HomePageSnapshotModel
{
    /// <summary>
    /// The latest news rows for the landing page (legacy newIndex takes the newest
    /// <see cref="Composition.LegacyDisplayRules.LandingPageNewsCount"/> ordered by Date descending,
    /// each subtitle already trimmed via <see cref="Composition.LegacyTextTransform.TrimForLandingPage"/>).
    /// </summary>
    public required IReadOnlyList<NewsSummaryModel> LatestNews { get; init; }

    /// <summary>
    /// Publication summaries for the exhibition section, ordered by StartDate descending and uncapped,
    /// matching the legacy newIndex <c>ViewBag.ExhibitionList</c>.
    /// </summary>
    public required IReadOnlyList<PublicationSummaryModel> FeaturedPublications { get; init; }

    /// <summary>
    /// 后台勾选「首页推荐」的展览（已按 FeaturedSort 排序）。由 <c>HomeController.Index</c> 通过
    /// <c>IPublicationReadRepository.GetHomeFeaturedAsync</c> 单独填充；旧库缺列 / 没有推荐数据时为空，
    /// 首页回退到原有硬编码展览块。
    /// </summary>
    public IReadOnlyList<FeaturedExhibitionModel> FeaturedExhibitions { get; set; } = Array.Empty<FeaturedExhibitionModel>();

    /// <summary>
    /// Legacy newIndex section06 renders rows flagged <c>Important</c> as featured blocks
    /// (cover image + trimmed subtitle + CTA). Per the legacy <c>newslist</c> rule, null counts as featured
    /// (<c>!= false</c>). Order is preserved from <see cref="LatestNews"/>.
    /// </summary>
    public IReadOnlyList<NewsSummaryModel> ImportantNews =>
        LatestNews.Where(news => news.Important != false).ToArray();

    /// <summary>
    /// Legacy newIndex section06 renders rows that are not <c>Important</c> as date + title list rows
    /// (only explicit <c>false</c>). Order is preserved from <see cref="LatestNews"/>.
    /// </summary>
    public IReadOnlyList<NewsSummaryModel> NormalNews =>
        LatestNews.Where(news => news.Important == false).ToArray();
}
