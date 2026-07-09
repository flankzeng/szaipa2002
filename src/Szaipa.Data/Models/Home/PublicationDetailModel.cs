namespace Szaipa.Data.Models.Home;

public sealed class PublicationDetailModel
{
    public int Id { get; init; }

    public string TitleCn { get; init; } = string.Empty;

    public string TitleEn { get; init; } = string.Empty;

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string FolderName { get; init; } = string.Empty;

    /// <summary>
    /// Number of swiper images beyond the first; legacy <c>Publication.cshtml</c> loops the gallery as
    /// <c>10001..10001 + MaxImg</c> (and the thumbnail strip <c>10000..10000 + MaxImg</c>).
    /// </summary>
    public int MaxImg { get; init; }

    public string CoverPath { get; init; } = string.Empty;

    public string LogoPath { get; init; } = string.Empty;

    public string MaxImagePath { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string Organizer { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public string CoHost { get; init; } = string.Empty;

    public string EditRecord { get; init; } = string.Empty;

    /// <summary>Template type: 0 = 普通 (simple gallery), 1 = 重要 (banner + preface skin).</summary>
    public int Type { get; init; }

    public string Preface { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    /// <summary>Optional 参展作品目录 rows (重要 skin only), ordered by SortOrder. Empty when none configured.</summary>
    public IReadOnlyList<ExhibitionWorkModel> Works { get; init; } = Array.Empty<ExhibitionWorkModel>();
}
