namespace Szaipa.Data.Models.Home;

public sealed class ArtistDetailModel
{
    public int Id { get; init; }

    public string ArtistNameCn { get; init; } = string.Empty;

    public string ArtistNameEn { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Nation { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Honor { get; init; } = string.Empty;

    public string Introduction { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Path1 { get; init; } = string.Empty;

    public string Path2 { get; init; } = string.Empty;

    /// <summary>Legacy <c>Artist.Position</c> — the artist's social positions block on newArt.</summary>
    public string Position { get; init; } = string.Empty;

    /// <summary>Legacy <c>Artist.DeedsThings</c> — the rich-HTML chronology shown on newArt (rendered raw).</summary>
    public string DeedsThings { get; init; } = string.Empty;

    /// <summary>Legacy <c>Artist.Color1</c> — first stop of the newArt header background gradient.</summary>
    public string Color1 { get; init; } = string.Empty;

    /// <summary>Legacy <c>Artist.Color2</c> — second stop of the newArt header background gradient.</summary>
    public string Color2 { get; init; } = string.Empty;
}
