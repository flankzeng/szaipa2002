namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Diary</c> table: per-day visit tallies plus an operation-record
/// log. Property names mirror the EF6 Database-First model (note the legacy <c>VistiTotal</c> spelling).
/// </summary>
public sealed class Diary
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public int VistiTotal { get; set; }

    public int NewsVisit { get; set; }

    public int ArtVisit { get; set; }

    public int WorksVisit { get; set; }

    public int CompanyVisit { get; set; }

    public string? OperationRecord { get; set; }
}
