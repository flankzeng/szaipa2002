namespace Szaipa.Data.Models.Home;

public sealed class ArtistArchiveSnapshotModel
{
    public required ArtistDetailModel Artist { get; init; }
    public required IReadOnlyList<ArtNewsSummaryModel> News { get; init; }
    public required IReadOnlyList<ArtistFavoriteModel> Favorites { get; init; }
    public required IReadOnlyList<ArtistAuctionModel> Auctions { get; init; }
}
