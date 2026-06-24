namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Activity</c> table. Property names mirror the EF6 Database-First
/// model so a later <c>dotnet ef dbcontext scaffold</c> diffs cleanly.
/// </summary>
public sealed class Activity
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public int VisitCount { get; set; }

    public string? Class { get; set; }

    public string? RArt { get; set; }

    public string? RWork { get; set; }

    public string? RNews { get; set; }

    public string? CoverTitle { get; set; }

    public string? ImgsTitle { get; set; }

    public string? Add { get; set; }

    public string? OtherInf { get; set; }
}
