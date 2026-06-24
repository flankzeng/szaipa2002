using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side repository for the staff backend's Exhibition (Publication) module.</summary>
public interface IPublicationAdminRepository
{
    Task<PagedResult<Publication>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<Publication?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(Publication input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Updates just MaxImg after the gallery is rebuilt (keeps the row in sync with the image count).</summary>
    Task<bool> SetMaxImgAsync(int id, int maxImg, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);
}
