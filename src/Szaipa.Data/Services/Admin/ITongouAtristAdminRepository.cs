using Szaipa.Data.Contexts.Tongou;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the 同构(Tongou) Atrist module (over <c>TongouAdminContext</c>).</summary>
public interface ITongouAtristAdminRepository
{
    Task<PagedResult<TongouAtrist>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<TongouAtrist?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Mirrors the legacy AtristAdd uniqueness check: true if an artist with this name already exists.</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    /// <summary>Creates an artist row from the editable fields of <paramref name="input"/>; returns the new Id.</summary>
    Task<int> CreateAsync(TongouAtrist input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/> to the stored row. False if not found.</summary>
    Task<bool> UpdateAsync(TongouAtrist input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken);
}
