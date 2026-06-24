using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's ArtNews (per-artist news) module.</summary>
public interface IArtNewsAdminRepository
{
    Task<PagedResult<ArtNewsListItem>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ArtNews?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken);

    Task<int> CreateAsync(ArtNews input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(ArtNews input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);
}
