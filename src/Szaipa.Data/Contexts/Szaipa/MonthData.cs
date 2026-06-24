namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.MonthData</c> table: a serialized monthly visit-data blob.
/// Property names mirror the EF6 Database-First model.
/// </summary>
public sealed class MonthData
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Data { get; set; }
}
