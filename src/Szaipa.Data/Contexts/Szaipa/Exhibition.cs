namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Read-only entity for the legacy <c>dbo.Exhibition</c> table (the per-artist exhibition feed on newArt).
/// Note <c>StartDate</c>/<c>EndDate</c> are <see cref="string"/> in this legacy table, unlike Publication.
/// </summary>
public sealed class Exhibition : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string? CoverPath { get; set; }

    public string? Title { get; set; }

    public string? Location { get; set; }

    public string? StartDate { get; set; }

    public string? Link { get; set; }

    public string? EndDate { get; set; }

    public string? EditRecord { get; set; }

    public int? VisitCount { get; set; }
}
