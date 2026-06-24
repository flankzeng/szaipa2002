namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Writable entity for the legacy <c>dbo.AccessData</c> table: per-visit geo records feeding the staff
/// visit-analytics charts. Property names mirror the EF6 Database-First model (note the legacy
/// <c>Porvince</c> / <c>Ctiy</c> spellings).
/// </summary>
public sealed class AccessData
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public string? Ip { get; set; }

    public string? Nation { get; set; }

    public string? Porvince { get; set; }

    public string? Ctiy { get; set; }

    public string? DC { get; set; }
}
