namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.Artist</c> table (translated from the EF6 model).</summary>
public sealed class Artist
{
    public int Id { get; set; }

    public string? ArtistNameCN { get; set; }

    public string? ArtistNameEN { get; set; }

    public string? Sex { get; set; }

    public string? Path { get; set; }

    public string? UserContent { get; set; }

    public string? Nation { get; set; }

    public string? City { get; set; }

    public string? Title { get; set; }

    public int? WorkCount { get; set; }

    public string? EditRecord { get; set; }

    public string? Deeds { get; set; }

    public string? Honor { get; set; }

    public DateTime? AddDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? FlieInf { get; set; }

    public int? VisitCount { get; set; }

    public string? Activity { get; set; }

    public string? Color1 { get; set; }

    public string? Color2 { get; set; }

    public string? Introduction { get; set; }

    public string? Position { get; set; }

    public string? Path1 { get; set; }

    public string? Path2 { get; set; }

    public string? DeedsThings { get; set; }

    public string? DeedsYears { get; set; }

    /// <summary>
    /// T4 新增列：true = 官网「相关展览」不再追加旧版写死的 3 张默认活动图（春语II/同构II/春语I）。
    /// 故意取「隐藏」语义而不是「显示」：这样 CLR 默认值 false 与数据库默认值 0 一致，老艺术家
    /// 全部保持原样（仍显示默认三张），新建艺术家由后台创建时置 true。
    /// </summary>
    public bool HideDefaultExhibitions { get; set; }
}
