using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>Write-side contract shared by the ordered homepage/About block modules.</summary>
public interface ISortableAdminRepository<T>
    where T : class, ISortableRecord
{
    /// <summary>All rows (hidden ones included), in display order.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken);

    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Creates a row from the editable fields of <paramref name="input"/>, appended to the end of its scope; returns the new Id.</summary>
    Task<int> CreateAsync(T input, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Applies the editable fields of <paramref name="input"/>. False if not found.</summary>
    Task<bool> UpdateAsync(T input, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Moves a row one place up (direction &lt; 0) or down (&gt; 0) inside its scope.</summary>
    Task<bool> MoveAsync(int id, int direction, AdminActor actor, CancellationToken cancellationToken);

    Task<bool> SetVisibleAsync(int id, bool visible, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>Artists (with avatar file name) for the "关联艺术家" dropdown.</summary>
    Task<IReadOnlyList<ArtistAvatarOption>> GetArtistOptionsAsync(CancellationToken cancellationToken);
}

/// <summary>Write-side repository for the org chart (<see cref="OrgMember"/>); ordering is per <see cref="OrgMember.GroupName"/>.</summary>
public interface IOrgMemberAdminRepository : ISortableAdminRepository<OrgMember>
{
}

/// <summary>Write-side repository for the homepage 在售艺术品 carousel (<see cref="SaleWork"/>).</summary>
public interface ISaleWorkAdminRepository : ISortableAdminRepository<SaleWork>
{
}

/// <summary>Write-side repository for the About page 合作伙伴 (<see cref="Partner"/>).</summary>
public interface IPartnerAdminRepository : ISortableAdminRepository<Partner>
{
}
