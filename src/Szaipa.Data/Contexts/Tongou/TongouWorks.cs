namespace Szaipa.Data.Contexts.Tongou;

/// <summary>
/// Read-only entity for the legacy <c>dbo.TongouWorks</c> table (translated from the EF6 model).
/// Note the lowercase <c>id</c> primary key and the <c>Atristid</c> foreign-key column (legacy spelling).
/// </summary>
public sealed class TongouWorks
{
    public int id { get; set; }

    public int Atristid { get; set; }

    public string? AtristidName { get; set; }

    public string? Title { get; set; }

    public string? ImgPath { get; set; }

    public string? Size { get; set; }

    public int VisityCount { get; set; }

    public string? Type { get; set; }

    public string? CreationDate { get; set; }

    public int HotCount { get; set; }
}
