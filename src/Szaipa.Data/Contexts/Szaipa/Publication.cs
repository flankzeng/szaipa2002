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

    /// <summary>Template type: 0 = 普通 (simple gallery), 1 = 重要 (banner + preface + gallery skin).</summary>
    public int Type { get; set; }

    /// <summary>序 / preface body text shown on the 重要 skin.</summary>
    public string? Preface { get; set; }

    /// <summary>序 signature line (e.g. author + date) shown under the preface.</summary>
    public string? Signature { get; set; }
}
