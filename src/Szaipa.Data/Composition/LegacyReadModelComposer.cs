using Szaipa.Data.Models.Home;
using Szaipa.Data.Models.Tongou;

namespace Szaipa.Data.Composition;

public static class LegacyReadModelComposer
{
    public static HomePageSnapshotModel CreateHomePageSnapshot(
        IReadOnlyList<NewsSummaryModel> latestNews,
        IReadOnlyList<PublicationSummaryModel> featuredPublications)
    {
        return new HomePageSnapshotModel
        {
            LatestNews = latestNews
                .Select(item => new NewsSummaryModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Subtitle = LegacyTextTransform.TrimForLandingPage(item.Subtitle),
                    Date = item.Date,
                    CoverPath = item.CoverPath,
                    Important = item.Important
                })
                .ToArray(),
            FeaturedPublications = featuredPublications
        };
    }

    public static PublicationDetailSnapshotModel CreatePublicationDetailSnapshot(
        PublicationDetailModel publication,
        IReadOnlyList<PublicationSummaryModel> relatedPublications)
    {
        return new PublicationDetailSnapshotModel
        {
            Publication = publication,
            RelatedPublications = relatedPublications
        };
    }

    public static ArtistProfileSnapshotModel CreateArtistProfileSnapshot(
        ArtistDetailModel artist,
        IReadOnlyList<WorkSummaryModel> works,
        IReadOnlyList<ArtistSidePanelItemModel> latestNews,
        IReadOnlyList<ArtistSidePanelItemModel> latestPublications,
        IReadOnlyList<ArtistSidePanelItemModel> latestFavorites,
        IReadOnlyList<ArtistSidePanelItemModel> latestAuctions,
        IReadOnlyList<ArtistExhibitionItemModel> relatedExhibitions)
    {
        return new ArtistProfileSnapshotModel
        {
            Artist = artist,
            Works = works,
            LatestNews = latestNews,
            LatestPublications = latestPublications,
            LatestFavorites = latestFavorites,
            LatestAuctions = latestAuctions,
            RelatedExhibitions = relatedExhibitions
        };
    }

    public static TongouArtistProfileSnapshotModel CreateTongouArtistProfileSnapshot(
        TongouArtistModel artist,
        IReadOnlyList<TongouWorkModel> works)
    {
        return new TongouArtistProfileSnapshotModel
        {
            Artist = artist,
            Works = works
        };
    }
}
