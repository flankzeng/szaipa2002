namespace Szaipa.Data.Models.Home;

public sealed class ArtistFavoriteModel
{
    public int Id { get; init; }
    public int ArtistId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string CollectNumber { get; init; } = string.Empty;
}
