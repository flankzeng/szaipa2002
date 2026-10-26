namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Read-only entity for the legacy <c>dbo.Publication</c> table. Property names mirror the EF6
/// Database-First model (and the underlying columns), including the lowercase organizer columns
/// <c>zhuban</c>/<c>chengban</c>/<c>xieban</c>.
/// </summary>
public sealed class Publication
{
    public int Id { get; set; }

    public string? TitleCN { get; set; }

    public string? TitleEN { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? FolderName { get; set; }

    public int MaxImg { get; set; }

    public string? LogoPath { get; set; }

    public string? CoverPath { get; set; }

    public int ReadCount { get; set; }

    public string? EditRecord { get; set; }

    public bool? Status { get; set; }

    public string? Location { get; set; }

    public string? zhuban { get; set; }

    public string? chengban { get; set; }

    public string? xieban { get; set; }

    // --- Exhibition template fields (added 2026-06 for the data-driven exhibition admin) ---

    /// <summary>Template type: 0 = 普通 (simple gallery), 1 = 重要 (banner + preface + gallery skin),
    /// 2 = 专题 (block-assembled topic page, see <see cref="PublicationBlock"/> and <see cref="PublicationTypes"/>).</summary>
    public int Type { get; set; }

    /// <summary>序 / preface body text shown on the 重要 skin.</summary>
    public string? Preface { get; set; }

    /// <summary>序 signature line (e.g. author + date) shown under the preface.</summary>
    public string? Signature { get; set; }

    // --- Exhibition automation fields (2026-10-26, docs/sql/2026-10-26-exhibition-automation.sql) ---
    // All additive: an un-migrated database lacks these columns, so every read of them goes through a
    // guarded, separate query (PublicationReadRepository) and falls back to the previous behaviour.

    /// <summary>首页推荐：勾选后展览出现在首页「展会活动」卡片与轮播。</summary>
    public bool IsFeatured { get; set; }

    /// <summary>首页推荐排序，数值越小越靠前（仅 <see cref="IsFeatured"/> 为真时有意义）。</summary>
    public int FeaturedSort { get; set; }

    /// <summary>首页展览卡片的轮播图文件名（逗号分隔，位于 <c>/Content/images/{FolderName}/</c>）；
    /// 留空则取画廊前 7 张。</summary>
    public string? FeaturedImages { get; set; }

    /// <summary>专题页主题色（#RRGGBB），用于标题、分隔线、引言等强调色；空则用站点红。</summary>
    public string? ThemeColor { get; set; }

    /// <summary>专题页头部横幅图（<c>/Content/...</c> 完整地址）；空则回退封面 / 文件夹 10000.jpg。</summary>
    public string? BannerPath { get; set; }
}
