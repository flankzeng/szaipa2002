namespace Szaipa.Data.Models.Tongou;

public sealed class TongouWorkModel
{
    public int Id { get; init; }

    public int ArtistId { get; init; }

    public string ArtistName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ImgPath { get; init; } = string.Empty;

    // Optional display fields kept as raw nullable: the legacy Work view branches on `!= null`
    // (e.g. "创作年份"/"类型"/"尺寸" rows only render when the column is non-null).
    public string? Size { get; init; }

    public string? Type { get; init; }

    public string? CreationDate { get; init; }
}
