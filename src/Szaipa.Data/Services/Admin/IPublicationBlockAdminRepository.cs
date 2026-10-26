using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Item shape submitted by the admin topic-block editor for one 专题 page's block list.</summary>
public sealed record PublicationBlockInput(string? BlockType, string? Content);

/// <summary>
/// Write-side repository for a 专题 page's <see cref="PublicationBlock"/> list, managed on the Publication edit
/// page (not a standalone CRUD module). Same replace-on-save pattern as <see cref="IExhibitionWorkAdminRepository"/>.
/// </summary>
public interface IPublicationBlockAdminRepository
{
    Task<IReadOnlyList<PublicationBlock>> GetByPublicationAsync(int publicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the entire block list for <paramref name="publicationId"/> with <paramref name="items"/> in the
    /// given order (fresh contiguous SortOrder). Each item's JSON is normalised through
    /// <c>PublicationBlockContent.Normalize</c>; items with an unknown type are dropped. Returns the number saved.
    /// </summary>
    Task<int> ReplaceAsync(
        int publicationId,
        IReadOnlyList<PublicationBlockInput> items,
        AdminActor actor,
        CancellationToken cancellationToken);
}
