using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's Company module (over <c>SzaipaAdminContext</c>).</summary>
public interface ICompanyAdminRepository
{
    Task<PagedResult<Company>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<Company?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a company row from the editable fields of <paramref name="input"/>; returns the new Id.</summary>
    Task<int> CreateAsync(Company input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/> to the stored row. False if not found.</summary>
    Task<bool> UpdateAsync(Company input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);
}
