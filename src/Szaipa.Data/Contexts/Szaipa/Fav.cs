namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.Fav</c> table (translated from the EF6 model).</summary>
public sealed class Fav : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string? Title { get; set; }

    public string? CoverPath { get; set; }

    public string? Location { get; set; }

    public string? Year { get; set; }

    public string? EditRecord { get; set; }

    public string? Creator { get; set; }

    public string? Size { get; set; }

    public string? Material { get; set; }

    public string? Type { get; set; }

    public string? Province { get; set; }

    public string? CollectNumber { get; set; }

    public int? VisitCount { get; set; }
}
