namespace Szaipa.Data.Models.Home;

public sealed class ArtistArchiveSnapshotModel
{
    public required ArtistDetailModel Artist { get; init; }
    public required IReadOnlyList<ArtNewsSummaryModel> News { get; init; }
    public required IReadOnlyList<ArtistFavoriteModel> Favorites { get; init; }
    public required IReadOnlyList<ArtistAuctionModel> Auctions { get; init; }

    /// <summary>出版著作；<c>ArtistBook</c> 表不存在（老库未执行 SQL）或没有数据时为空列表。</summary>
    public IReadOnlyList<ArtistBookModel> Books { get; init; } = Array.Empty<ArtistBookModel>();
}
