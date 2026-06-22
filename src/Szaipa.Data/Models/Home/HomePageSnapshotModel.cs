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
