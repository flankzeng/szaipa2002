namespace Szaipa.Data.Models.Home;

public sealed class ArtistSummaryModel
{
    public int Id { get; init; }

    public string ArtistNameCn { get; init; } = string.Empty;

    public string ArtistNameEn { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Introduction { get; init; } = string.Empty;
}
