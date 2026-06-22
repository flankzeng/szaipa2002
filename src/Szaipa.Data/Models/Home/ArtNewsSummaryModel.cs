namespace Szaipa.Data.Models.Home;

public sealed class ArtNewsSummaryModel
{
    public int Id { get; init; }

    public int ArtistId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public string CoverPath { get; init; } = string.Empty;
}
