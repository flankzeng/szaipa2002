namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Read-only entity for the legacy <c>dbo.News</c> table. Property names mirror the EF6 Database-First
/// model (and the underlying columns) so a later <c>dotnet ef dbcontext scaffold</c> against a verified
/// read-only connection diffs cleanly against this hand-authored translation.
/// </summary>
public sealed class News
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public string? Autor { get; set; }

    public bool original { get; set; }

    public string? link { get; set; }

    public DateTime? Date { get; set; }

    public string? Content { get; set; }

    public string? CoverPath { get; set; }

    public string? EditRecord { get; set; }

    public int ReadCount { get; set; }

    public string? ImgTitle { get; set; }

    public string? Activity { get; set; }

    public bool? Important { get; set; }

    public int? Year { get; set; }
}
