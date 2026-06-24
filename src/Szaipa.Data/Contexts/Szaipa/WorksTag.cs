namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.WorksTag</c> join table linking works to tags. Property names
/// mirror the EF6 Database-First model.
/// </summary>
public sealed class WorksTag
{
    public int Id { get; set; }

    public int WorkId { get; set; }

    public int TagId { get; set; }
}
