namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.Tag</c> table (work tags / search hotness). Property names mirror
/// the EF6 Database-First model (note the legacy <c>SerachHot</c> spelling).
/// </summary>
public sealed class Tag
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public int WorksCount { get; set; }

    public int SerachHot { get; set; }
}
