using Szaipa.Data.Contexts.Tongou;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the 同构(Tongou) Works module (over <c>TongouAdminContext</c>).</summary>
public interface ITongouWorksAdminRepository
{
    Task<PagedResult<TongouWorkListItem>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<TongouWorks?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a work row from the editable fields of <paramref name="input"/>; returns the new Id.</summary>
    Task<int> CreateAsync(TongouWorks input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/> to the stored row. False if not found.</summary>
    Task<bool> UpdateAsync(TongouWorks input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtistOption>> GetArtistOptionsAsync(CancellationToken cancellationToken);
}
