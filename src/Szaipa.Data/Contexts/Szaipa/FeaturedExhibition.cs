using System.ComponentModel.DataAnnotations;
using Szaipa.Data.Services;

namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 首页「推荐展览」一条记录（<c>dbo.FeaturedExhibition</c>）。驱动首页 #section07「展会活动」卡片，
/// 以及（<see cref="ShowInCarousel"/> 为真时）#section02 顶部轮播。
///
/// <para>为什么是独立的小表而不是 Publication 上的列：生产 Publication 表目前是 0 行，所有展览都来自
/// <c>LegacyPublicationGalleryCatalog</c> 的只读兼容数据（固定 id 92001–92015）、三个手写专题页和首页硬编码。
/// 推荐记录因此必须是<b>自包含</b>的卡片数据（标题 / 链接 / 信息行 / 图片），它只通过 <see cref="LinkUrl"/>
/// 指向目标页面（<c>/Home/Publication/92011</c>、<c>/Publication/chunyu3</c> 或任何站内地址），
/// 不依赖 Publication 行是否存在——slug 迁移 SQL 执行前后首页都一样。</para>
/// </summary>
public sealed class FeaturedExhibition : IValidatableObject
{
    public int Id { get; set; }

    /// <summary>显示顺序，数值越小越靠前。</summary>
    public int SortOrder { get; set; }

    /// <summary>取消勾选 = 不在首页显示（但仍占用对应链接，不会让同链接的旧硬编码块复活）。</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>是否同时出现在 #section02 顶部轮播（需要 <see cref="CoverUrl"/>）。</summary>
    public bool ShowInCarousel { get; set; }

    /// <summary>展览名（卡片 h1 / 轮播竖排标题）。</summary>
    [Required(ErrorMessage = "请输入推荐标题")]
    [StringLength(300, ErrorMessage = "标题不能超过300个字符")]
    public string Title { get; set; } = string.Empty;

    /// <summary>轮播竖排标题后的年份，如 <c>2024</c>。</summary>
    [StringLength(40, ErrorMessage = "年份不能超过40个字符")]
    public string? YearText { get; set; }

    /// <summary>站内目标地址，必须以 <c>/</c> 开头。</summary>
    [Required(ErrorMessage = "请输入目标链接")]
    [StringLength(1000, ErrorMessage = "链接不能超过1000个字符")]
    public string LinkUrl { get; set; } = string.Empty;

    /// <summary>卡片信息行，每行一条原样输出，如「展览时间：2024.05.24-2024.05.31」。</summary>
    public string? InfoLines { get; set; }

    /// <summary>卡片轮播图，每行一个 <c>/Content/...</c> 地址（首页只取前 7 张）。</summary>
    public string? ImageUrls { get; set; }

    /// <summary>轮播 Logo 图地址。</summary>
    [StringLength(1000)]
    public string? LogoUrl { get; set; }

    /// <summary>轮播大图地址。</summary>
    [StringLength(1000)]
    public string? CoverUrl { get; set; }

    /// <summary>Standalone and slug-card dates; internal Publication links resolve their own row/catalog dates first.</summary>
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>true = automatic date lifecycle; false = force ended; null = offline.</summary>
    public bool? Status { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!FeaturedExhibitionContent.IsSafeLink(LinkUrl))
        {
            yield return new ValidationResult("链接必须是安全的本站路径。", new[] { nameof(LinkUrl) });
        }
    }
}
