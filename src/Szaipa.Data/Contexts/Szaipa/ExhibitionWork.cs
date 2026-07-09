namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// A single row in an exhibition's optional "参展作品目录" (works catalog) — shown on the 重要 (important)
/// exhibition skin between the 序 and the on-site photo gallery. Unlike the gallery's file-system-numbered
/// images, each row is a full record (category/title/artist/size/medium/image). The whole set for a
/// publication is rewritten wholesale on every admin save (delete + reinsert with a fresh contiguous
/// <see cref="SortOrder"/>), mirroring the gallery's replace-on-save pattern without needing gap-free file
/// numbering.
/// </summary>
public sealed class ExhibitionWork
{
    public int Id { get; set; }

    /// <summary>FK to <see cref="Publication.Id"/>. No EF navigation property — this layer's entities stay
    /// flat/column-shaped, consistent with the rest of <c>Contexts/Szaipa</c>.</summary>
    public int PublicationId { get; set; }

    public string? Category { get; set; }

    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? Size { get; set; }

    public string? Medium { get; set; }

    public string? ImagePath { get; set; }

    /// <summary>0-based display order within the publication.</summary>
    public int SortOrder { get; set; }
}
