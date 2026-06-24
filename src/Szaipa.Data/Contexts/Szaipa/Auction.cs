namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.Auction</c> table (translated from the EF6 model).</summary>
public sealed class Auction : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string? Title { get; set; }

    public string? CoverPath { get; set; }

    public string? Price { get; set; }

    public string? RMB { get; set; }

    public string? HKD { get; set; }

    public string? USD { get; set; }

    public string? Date { get; set; }

    public string? EditRecord { get; set; }

    public int? VisitCount { get; set; }
}
