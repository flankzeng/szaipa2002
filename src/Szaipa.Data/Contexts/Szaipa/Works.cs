namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>Read-only entity for the legacy <c>dbo.Works</c> table (translated from the EF6 model).</summary>
public sealed class Works : IArtistScopedRecord
{
    public int Id { get; set; }

    public int ArtistId { get; set; }

    public string? Title { get; set; }

    public string? Path { get; set; }

    public string? Content { get; set; }

    public string? Record { get; set; }

    public int? VisitCount { get; set; }

    public DateTime? FirstDate { get; set; }

    public DateTime? LastDate { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool transverse { get; set; }

    public bool @long { get; set; }

    public string? Activity { get; set; }

    public string? Tags { get; set; }

    public string? Deeds { get; set; }

    public string? EditRecord { get; set; }

    // --- 艺术家官网一站式编辑（T4，2026-10）新增列 ---

    /// <summary>
    /// 作品在艺术家官网「作品集」里的显示顺序（升序；同值按 Id）。老库没有这一列（脚本
    /// docs/sql/2026-10-26-artist-site.sql 补），前台读侧缺列时回退到旧的按 Id 顺序。
    /// </summary>
    public int Sort { get; set; }
}
