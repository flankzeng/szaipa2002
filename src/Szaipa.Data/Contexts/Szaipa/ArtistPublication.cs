namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 艺术家 ↔ 展览（<see cref="Publication"/>）的关联表（T4，2026-10 新增）。艺术家官网「相关展览 / 展讯」区
/// 按 <see cref="Sort"/> 升序渲染这些展览的封面/场馆/日期/链接，取代 Art.cshtml 里写死的活动图。
/// 与 <see cref="ExhibitionWork"/> 一样「整组替换」：后台每次保存先删该艺术家的全部行再按新顺序重插。
/// 实体保持扁平（无导航属性）。老库没有此表，前台读侧缺表时退化为只显示 Exhibition 行 + 旧默认三张。
/// </summary>
public sealed class ArtistPublication
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    /// <summary>FK → <see cref="Publication.Id"/>。</summary>
    public int PublicationId { get; set; }

    /// <summary>0 起的显示顺序。</summary>
    public int Sort { get; set; }
}
