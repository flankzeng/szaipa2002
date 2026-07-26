namespace Szaipa.Data.Models.Home;

public sealed class ArtistAuctionModel
{
    public int Id { get; init; }
    public int ArtistId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;
    public string Price { get; init; } = string.Empty;
    public string Rmb { get; init; } = string.Empty;
    public string Hkd { get; init; } = string.Empty;
    public string Usd { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
}
