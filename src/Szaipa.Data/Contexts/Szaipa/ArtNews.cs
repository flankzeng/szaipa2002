namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.ArtNews</c> table (translated from the EF6 model).</summary>
public sealed class ArtNews
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public DateTime? Date { get; set; }

    public string? CoverPath { get; set; }

    public int? ReadCount { get; set; }

    public string? Content { get; set; }

    public string? Title { get; set; }

    public string? EditRecord { get; set; }

    public string? ImgTitle { get; set; }

    public string? SubTitle { get; set; }
}
