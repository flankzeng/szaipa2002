using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's Artist module (over <c>SzaipaAdminContext</c>).</summary>
public interface IArtistAdminRepository
{
    Task<PagedResult<Artist>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<Artist?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates an artist row from the editable fields of <paramref name="input"/>; returns the new Id.</summary>
    Task<int> CreateAsync(Artist input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/> to the stored row. False if not found.</summary>
    Task<bool> UpdateAsync(Artist input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);
}
