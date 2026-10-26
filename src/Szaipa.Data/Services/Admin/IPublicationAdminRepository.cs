using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's Exhibition (Publication) module.</summary>
public interface IPublicationAdminRepository
{
    Task<PagedResult<Publication>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Same as above but optionally restricted to one <see cref="PublicationLifecycle"/> (evaluated against
    /// <paramref name="today"/> in SQL via <see cref="PublicationLifecycleCalculator.Matches"/>).
    /// </summary>
    Task<PagedResult<Publication>> GetPagedAsync(
        int page,
        int pageSize,
        PublicationLifecycle? lifecycle,
        DateTime today,
        CancellationToken cancellationToken);

    /// <summary>Number of exhibitions in each lifecycle state, for the admin filter tabs.</summary>
    Task<IReadOnlyDictionary<PublicationLifecycle, int>> GetLifecycleCountsAsync(DateTime today, CancellationToken cancellationToken);

    Task<Publication?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Updates just MaxImg after the gallery is rebuilt (keeps the row in sync with the image count).</summary>
    Task<bool> SetMaxImgAsync(int id, int maxImg, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);
}
