namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// One artwork in the homepage 在售艺术品 carousel (<c>#section5to6</c>). Caption renders as
/// "{ArtistName} 作品 / 《{Title}》 {Size} {Medium}". <see cref="ImagePath"/> is the ORIGINAL image; the page shows
/// the q30 preview (via the preview resolver) and keeps the original in <c>data-magnify-src</c> for the zoom.
/// Table is additive — readers must tolerate it not existing yet.
/// </summary>
public sealed class SaleWork : ISortableRecord
{
    public int Id { get; set; }

    /// <summary>Optional link to <see cref="Artist.Id"/>; when set the artist-name line jumps to /Home/Art/{id}#section02.</summary>
    public int? ArtistId { get; set; }

    /// <summary>Artist name shown in the caption (kept as text so unlinked artists work).</summary>
    public string? ArtistName { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>尺寸, e.g. 85x58cm.</summary>
    public string? Size { get; set; }

    /// <summary>材质, e.g. 纸本设色.</summary>
    public string? Medium { get; set; }

    /// <summary>Site-relative ORIGINAL image URL under /Content (e.g. /Content/ArtImg/selling/cxz.jpg).</summary>
    public string? ImagePath { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public int Sort { get; set; }

    public bool IsVisible { get; set; } = true;
}
