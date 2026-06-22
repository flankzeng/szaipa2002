namespace Szaipa.Data.Models.Home;

/// <summary>
/// One row of the per-artist "相关展览 / EXHIBITION" feed rendered by the legacy <c>newArt</c> page
/// (<c>db.Exhibition.Where(e =&gt; e.ArtistId == id)</c>). This is a different feed from the landing-page
/// exhibition list, which is sourced from <c>Publication</c>. Note the legacy <c>Exhibition</c> entity
/// stores <c>StartDate</c>/<c>EndDate</c> as <see cref="string"/>, not <see cref="System.DateTime"/>.
/// </summary>
public sealed class ArtistExhibitionItemModel
{
    public int Id { get; init; }

    public int ArtistId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string CoverPath { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string Link { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string EndDate { get; init; } = string.Empty;
}
