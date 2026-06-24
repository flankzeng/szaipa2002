namespace Szaipa.Data.Services.Admin;

/// <summary>List-row projection for the admin ArtNews list (joins the artist's Chinese name).</summary>
public sealed class ArtNewsListItem
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public int ArtistId { get; init; }

    public string? ArtistName { get; init; }

    public DateTime? Date { get; init; }
}

/// <summary>An artist choice for the ArtNews artist dropdown.</summary>
public sealed record ArtistOption(int Id, string Name);
