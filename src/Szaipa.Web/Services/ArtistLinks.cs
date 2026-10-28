namespace Szaipa.Web.Services;

/// <summary>
/// 前台指向艺术家首页的链接（首页成员卡、会员风采、在售作品）。2026-10-28 用户要求：没设计艺术家首页（留空）的人，
/// 点头像不跳转，链接为 <c>javascript:;;</c>（与旧站写法一致）。“有首页”的判定见
/// <c>IArtistReadRepository.GetArtistIdsWithHomepageAsync</c>：有作品或填了简介 / 职务 / 年表。
/// </summary>
public static class ArtistLinks
{
    public const string ViewDataKey = "ArtistIdsWithHomepage";

    public const string NoHomepage = "javascript:;;";

    /// <summary>
    /// <paramref name="withHomepage"/> 为 null（未取到判定数据，如读库未启用）时保持原链接，不误伤。
    /// </summary>
    public static string Href(int? artistId, IReadOnlySet<int>? withHomepage, string fragment = "") =>
        artistId is not { } id || (withHomepage is not null && !withHomepage.Contains(id))
            ? NoHomepage
            : $"/Home/Art/{id}{fragment}";

    public static bool HasHomepage(int? artistId, IReadOnlySet<int>? withHomepage) =>
        artistId is { } id && (withHomepage is null || withHomepage.Contains(id));
}
