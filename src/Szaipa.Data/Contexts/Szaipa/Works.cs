namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.Works</c> table (translated from the EF6 model).</summary>
public sealed class Works : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string? Title { get; set; }

    public string? Path { get; set; }

    public string? Content { get; set; }

    public string? Record { get; set; }

    public int? VisitCount { get; set; }

    public DateTime? FirstDate { get; set; }

    public DateTime? LastDate { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool transverse { get; set; }

    public bool @long { get; set; }

    public string? Activity { get; set; }

    public string? Tags { get; set; }

    public string? Deeds { get; set; }

    public string? EditRecord { get; set; }
}
