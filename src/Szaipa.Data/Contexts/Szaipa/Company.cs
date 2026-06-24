namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Company</c> table. Property names mirror the EF6 Database-First
/// model so a later <c>dotnet ef dbcontext scaffold</c> diffs cleanly.
/// </summary>
public sealed class Company
{
    public int Id { get; set; }

    public string? NameCN { get; set; }

    public string? NameEN { get; set; }

    public string? Business { get; set; }

    public string? CEO { get; set; }

    public string? ImgPath { get; set; }

    public string? FilePath { get; set; }

    public string? Address { get; set; }

    public string? EditRecord { get; set; }

    public int? VisitCount { get; set; }

    public DateTime? FirstDate { get; set; }

    public DateTime? LastDate { get; set; }

    public string? Activity { get; set; }
}
