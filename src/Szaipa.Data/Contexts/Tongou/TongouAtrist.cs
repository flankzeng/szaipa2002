namespace Szaipa.Data.Contexts.Tongou;

/// <summary>
/// Read-only entity for the legacy <c>dbo.TongouAtrist</c> table (translated from the EF6 model).
/// Note the primary key column is lowercase <c>id</c>, matching the legacy schema.
/// </summary>
public sealed class TongouAtrist
{
    public int id { get; set; }

    public string? Name { get; set; }

    public int WorksCount { get; set; }

    public int Aboutid { get; set; }

    public string? AboutText { get; set; }

    public string? Title { get; set; }

    public string? HeardPath { get; set; }

    public int HotCount { get; set; }
}
