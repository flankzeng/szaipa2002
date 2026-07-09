using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Item shape submitted by the admin works-catalog editor for one exhibition's works list.</summary>
public sealed record ExhibitionWorkInput(
    string? Category,
    string? Title,
    string? Artist,
    string? Size,
    string? Medium,
    string? ImagePath);

/// <summary>
/// Write-side repository for an exhibition's optional 参展作品目录 (works catalog) — a sub-list of
/// <see cref="ExhibitionWork"/> rows scoped to one <see cref="Publication"/>, managed alongside the
/// gallery images on the Publication edit page rather than as its own standalone CRUD module.
/// </summary>
public interface IExhibitionWorkAdminRepository
{
    Task<IReadOnlyList<ExhibitionWork>> GetByPublicationAsync(int publicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the entire works list for <paramref name="publicationId"/> with <paramref name="items"/> in
    /// the given order (existing rows are removed and reinserted with a fresh contiguous SortOrder), mirroring
    /// the gallery's replace-on-save pattern.
    /// </summary>
    Task ReplaceAsync(
        int publicationId,
        IReadOnlyList<ExhibitionWorkInput> items,
        AdminActor actor,
        CancellationToken cancellationToken);
}
