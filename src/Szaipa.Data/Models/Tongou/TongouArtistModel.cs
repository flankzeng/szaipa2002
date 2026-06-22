namespace Szaipa.Data.Models.Tongou;

public sealed class TongouArtistModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string HeardPath { get; init; } = string.Empty;

    public string AboutText { get; init; } = string.Empty;

    public int WorksCount { get; init; }
}
