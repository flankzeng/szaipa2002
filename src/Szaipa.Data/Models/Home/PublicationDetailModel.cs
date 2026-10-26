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

    /// <summary>Legacy <c>Status</c> flag (true = 自动按日期, false = 手动已结束, null = 下架); see <see cref="Composition.PublicationLifecycleCalculator"/>.</summary>
    public bool? Status { get; init; }

    /// <summary>Template type: 0 = 普通 (simple gallery), 1 = 重要 (banner + preface skin), 2 = 专题 (blocks).</summary>
    public int Type { get; init; }

    public string Preface { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    /// <summary>Optional 参展作品目录 rows (重要 skin only), ordered by SortOrder. Empty when none configured.</summary>
    public IReadOnlyList<ExhibitionWorkModel> Works { get; init; } = Array.Empty<ExhibitionWorkModel>();

    /// <summary>专题页主题色（#RRGGBB）；空 = 站点默认红。仅 Type == 2 读取。</summary>
    public string ThemeColor { get; init; } = string.Empty;

    /// <summary>专题页头部横幅图 URL；空 = 回退文件夹 10000.jpg。仅 Type == 2 读取。</summary>
    public string BannerPath { get; init; } = string.Empty;

    /// <summary>专题页积木（Type == 2），按 SortOrder 排好。表不存在 / 非专题时为空。</summary>
    public IReadOnlyList<PublicationBlockModel> Blocks { get; init; } = Array.Empty<PublicationBlockModel>();
}
