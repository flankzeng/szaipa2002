namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// One building block of a 专题 (<see cref="PublicationTypes.Topic"/>) exhibition page. A topic page is
/// assembled from an ordered list of blocks (banner / preface / rich text / works catalog / gallery /
/// video / quote) instead of a hand-written 1000-line view. The whole set for a publication is rewritten
/// wholesale on every admin save (delete + reinsert with a fresh contiguous <see cref="SortOrder"/>),
/// mirroring <see cref="ExhibitionWork"/>.
/// </summary>
public sealed class PublicationBlock
{
    public int Id { get; set; }

    /// <summary>FK to <see cref="Publication.Id"/>. No EF navigation property (flat, column-shaped entity).</summary>
    public int PublicationId { get; set; }

    /// <summary>0-based display order within the publication.</summary>
    public int SortOrder { get; set; }

    /// <summary>One of the <see cref="PublicationBlockTypes"/> constants.</summary>
    public string BlockType { get; set; } = string.Empty;

    /// <summary>Block payload as a JSON object; its shape depends on <see cref="BlockType"/>
    /// (see <see cref="PublicationBlockTypes"/>). Normalized/whitelisted on every admin save.</summary>
    public string Content { get; set; } = "{}";
}
