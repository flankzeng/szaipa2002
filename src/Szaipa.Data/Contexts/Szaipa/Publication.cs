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
}
