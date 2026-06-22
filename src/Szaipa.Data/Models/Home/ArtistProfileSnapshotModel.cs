namespace Szaipa.Data.Models.Home;

public sealed class ArtistProfileSnapshotModel
{
    public required ArtistDetailModel Artist { get; init; }

    public required IReadOnlyList<WorkSummaryModel> Works { get; init; }

    // Side-panel feeds. NOTE: legacy newArt takes each via an UNORDERED Take(1)
    // (db.X.Where(d => d.ArtistId == id).ToList().Take(1)) with NO OrderByDescending,
    // so it surfaces the FIRST row in default/PK order (lowest Id), not the newest.
    // The "Latest" names reflect the legacy Chinese comment (最新) intent; reproduce the
    // unordered single-row behavior for parity and do NOT add OrderByDescending(Date).

    public required IReadOnlyList<ArtistSidePanelItemModel> LatestNews { get; init; }

    public required IReadOnlyList<ArtistSidePanelItemModel> LatestPublications { get; init; }

    public required IReadOnlyList<ArtistSidePanelItemModel> LatestFavorites { get; init; }

    public required IReadOnlyList<ArtistSidePanelItemModel> LatestAuctions { get; init; }

    /// <summary>
    /// The per-artist exhibition feed (legacy newArt "相关展览 / EXHIBITION" swiper, sourced from
    /// <c>db.Exhibition.Where(e =&gt; e.ArtistId == id)</c>). Distinct from the landing-page exhibition
    /// list, which comes from <c>Publication</c>.
    /// </summary>
    public required IReadOnlyList<ArtistExhibitionItemModel> RelatedExhibitions { get; init; }
}
