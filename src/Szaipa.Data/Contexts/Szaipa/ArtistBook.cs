namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 艺术家「出版著作」（2026-10 新增表 <c>dbo.ArtistBook</c>）。取代旧站的占位 bug：Art 侧栏「出版著作」原先把
/// ArtNews 的资讯标题当书名显示，ArtArchive 的出版段则永远是「暂无出版著作资料」。
/// 老库没有此表：读侧缺表时侧栏/档案页显示空状态，后台模块显示「需先执行 SQL」，绝不 500。
/// 属于艺术家子模块（<see cref="IArtistScopedRecord"/>），封面存文件名，目录 <c>/Content/ArtImg/Artist/Book/</c>。
/// </summary>
public sealed class ArtistBook : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    /// <summary>书名（必填）。</summary>
    public string? Title { get; set; }

    public string? Publisher { get; set; }

    /// <summary>出版年份，文本（如 "2023"、"2021年9月"），不做日期校验。</summary>
    public string? PublishYear { get; set; }

    /// <summary>封面文件名（不含目录），目录为 <c>ArtImg/Artist/Book</c>。</summary>
    public string? CoverPath { get; set; }

    public string? Description { get; set; }

    /// <summary>显示顺序，小的在前；相同再按 Id。</summary>
    public int Sort { get; set; }

    public string? EditRecord { get; set; }
}
