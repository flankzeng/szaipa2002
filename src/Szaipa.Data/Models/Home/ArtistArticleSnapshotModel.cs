namespace Szaipa.Data.Models.Home;

public sealed class ArtistArticleSnapshotModel
{
    public required ArtistDetailModel Artist { get; init; }
    public required ArtNewsSummaryModel Article { get; init; }
}
