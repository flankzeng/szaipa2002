namespace Szaipa.Data.Models.Tongou;

public sealed class TongouArtistProfileSnapshotModel
{
    public required TongouArtistModel Artist { get; init; }

    public required IReadOnlyList<TongouWorkModel> Works { get; init; }
}
