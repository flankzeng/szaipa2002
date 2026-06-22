namespace Szaipa.Data.Models.Home;

public sealed class NewsDetailModel
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public string Content { get; init; } = string.Empty;

    public string CoverPath { get; init; } = string.Empty;
}
