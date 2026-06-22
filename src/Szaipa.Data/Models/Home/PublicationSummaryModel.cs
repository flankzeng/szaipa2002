namespace Szaipa.Data.Models.Home;

public sealed class PublicationSummaryModel
{
    public int Id { get; init; }

    public string TitleCn { get; init; } = string.Empty;

    public string TitleEn { get; init; } = string.Empty;

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string CoverPath { get; init; } = string.Empty;

    /// <summary>
    /// Legacy <c>Publication.LogoPath</c>; the landing carousel renders the exhibition logo at
    /// <c>/Content/images/{FolderName}/{LogoPath}</c>.
    /// </summary>
    public string LogoPath { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Legacy <c>Publication.FolderName</c>; the landing exhibition cards build their image carousel
    /// paths from it (<c>/Content/images/{FolderName}/{n}.jpg</c>), so it is required to render those cards.
    /// </summary>
    public string FolderName { get; init; } = string.Empty;

    /// <summary>Legacy <c>Publication.zhuban</c> (主办) — organizer line on the landing exhibition card.</summary>
    public string Organizer { get; init; } = string.Empty;

    /// <summary>Legacy <c>Publication.chengban</c> (承办) — host line on the landing exhibition card.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Legacy <c>Publication.xieban</c> (协办) — co-host line on the landing exhibition card.</summary>
    public string CoHost { get; init; } = string.Empty;

    /// <summary>
    /// Legacy <c>Publication.Status</c> (nullable). The landing page partitions the StartDate-desc list:
    /// <c>Status == true</c> renders in the active/"processing" section, <c>Status == false</c> in the ended
    /// section, and <c>null</c> is dropped from both. Kept nullable to reproduce that exact render gate.
    /// </summary>
    public bool? Status { get; init; }
}
