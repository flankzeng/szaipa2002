using Szaipa.Data.Models.Home;

namespace Szaipa.Web.Models;

/// <summary>
/// Drives the shared <c>_ExhibitionGallery</c> partial: an exhibition's title block plus the dual-swiper
/// image gallery. Images follow the legacy convention <c>/Content/images/{FolderName}/{n}.jpg</c>, numbered
/// from 10001 (the main swiper) and 10000 (the thumbnail swiper, where 10000 is the cover).
/// </summary>
public sealed class ExhibitionGalleryModel
{
    public string? TitleCn { get; init; }

    public string? TitleEn { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string? FolderName { get; init; }

    public int MaxImg { get; init; }

    /// <summary>序 / preface body (重要 skin only). May contain newlines for multiple paragraphs.</summary>
    public string? Preface { get; init; }

    /// <summary>序 signature line (重要 skin only).</summary>
    public string? Signature { get; init; }

    /// <summary>Optional 参展作品目录 rows (重要 skin only), already ordered. Empty when none configured.</summary>
    public IReadOnlyList<ExhibitionWorkModel> Works { get; init; } = Array.Empty<ExhibitionWorkModel>();
}
