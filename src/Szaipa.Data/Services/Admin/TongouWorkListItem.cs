namespace Szaipa.Data.Services.Admin;

/// <summary>List-row projection for the Tongou Works admin list, joined to the artist's name.</summary>
public sealed class TongouWorkListItem
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public int ArtistId { get; init; }

    public string? ArtistName { get; init; }
}
