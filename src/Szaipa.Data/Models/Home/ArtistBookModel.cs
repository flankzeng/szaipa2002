namespace Szaipa.Data.Models.Home;

/// <summary>艺术家档案页「出版著作」一条书目（<c>ArtistBook</c> 投影）。</summary>
public sealed class ArtistBookModel
{
    public int Id { get; init; }
    public int ArtistId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string PublishYear { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
