namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 会员风采（/Home/Vip）的展示设置（2026-10 新增表 <c>dbo.ArtistDisplay</c>）。刻意做成独立旁表而不是给 Artist 加列：
/// 生产库的 Artist 表不动，只影响 Vip 列表，艺术家官网页本身不受「隐藏」影响。
/// 一位艺术家最多一行（<see cref="ArtistId"/> 即主键，不自增）；没有行的艺术家按原 Id 倒序排在有行者之后。
/// 老库没有此表：读侧缺表时 Vip 页完全回退原顺序，后台模块显示「需先执行 SQL」，绝不 500。
/// </summary>
public sealed class ArtistDisplay
{
    public int ArtistId { get; set; }

    /// <summary>显示顺序，小的在前；相同再按 Id 倒序。</summary>
    public int Sort { get; set; }

    /// <summary>true 则不出现在 Vip 列表（艺术家官网页仍可访问）。</summary>
    public bool IsHidden { get; set; }
}
