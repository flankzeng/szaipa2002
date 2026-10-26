namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Values of <see cref="Publication.Type"/>.</summary>
public static class PublicationTypes
{
    /// <summary>普通：标题 + 画廊。</summary>
    public const int Normal = 0;

    /// <summary>重要：banner + 序 + 作品目录 + 画廊。</summary>
    public const int Important = 1;

    /// <summary>专题：由 <see cref="PublicationBlock"/> 积木拼装。</summary>
    public const int Topic = 2;
}

/// <summary>
/// Allowed <see cref="PublicationBlock.BlockType"/> values and the JSON keys each one understands
/// (everything else is dropped on save by <c>PublicationBlockContent</c>).
/// <list type="bullet">
///   <item><c>banner</c>: <c>imagePath</c>, <c>title</c>, <c>subtitle</c> — 通栏横幅（章节分隔）。</item>
///   <item><c>preface</c>: <c>heading</c>, <c>body</c> (plain text, line break = new paragraph), <c>signature</c> — 序言。</item>
///   <item><c>richtext</c>: <c>heading</c>, <c>html</c> (TipTap HTML, sanitized) — 富文本。</item>
///   <item><c>works</c>: <c>heading</c> — 参展作品目录（数据来自 ExhibitionWork，在展览页「参展作品目录」里维护）。</item>
///   <item><c>gallery</c>: <c>heading</c> — 现场图库（数据来自展览图片文件夹，仅第一个生效）。</item>
///   <item><c>video</c>: <c>source</c> (<c>local</c>|<c>embed</c>), <c>url</c>, <c>poster</c>, <c>caption</c> — 视频。</item>
///   <item><c>quote</c>: <c>text</c>, <c>author</c> — 引言。</item>
/// </list>
/// </summary>
public static class PublicationBlockTypes
{
    public const string Banner = "banner";
    public const string Preface = "preface";
    public const string RichText = "richtext";
    public const string Works = "works";
    public const string Gallery = "gallery";
    public const string Video = "video";
    public const string Quote = "quote";

    public static readonly IReadOnlyList<string> All =
        new[] { Banner, Preface, RichText, Works, Gallery, Video, Quote };

    public static bool IsKnown(string? blockType) =>
        blockType is not null && All.Contains(blockType, StringComparer.Ordinal);
}
