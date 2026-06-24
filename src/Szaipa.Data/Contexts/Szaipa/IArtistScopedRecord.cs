namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Shared shape of the artist-scoped child records (Works/Fav/Auction/Exhibition): every one has an identity,
/// belongs to an artist, has a title, and carries an append-only edit-record audit string. This lets the
/// admin write side share one generic repository for the common list/create/update/delete + logging flow.
/// </summary>
public interface IArtistScopedRecord
{
    int Id { get; set; }

    int ArtistId { get; set; }

    string? Title { get; set; }

    string? EditRecord { get; set; }
}
