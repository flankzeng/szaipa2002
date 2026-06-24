namespace Szaipa.Data.Services.Admin;

/// <summary>List-row projection shared by the artist-scoped admin modules (joins the artist's Chinese name).</summary>
public sealed class ArtistScopedListItem
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public int ArtistId { get; init; }

    public string? ArtistName { get; init; }
}
