using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

/// <summary>
/// Read side of the data-driven homepage static blocks (核心成员 / 顾问团队 / 在售艺术品) and the About page's
/// 合作伙伴. Both methods are total: if the backing tables do not exist yet (un-migrated database) or hold no
/// rows at all, they return the built-in <see cref="Composition.HomeBlocksDefaults"/> (the previously
/// hard-coded content) instead of throwing, so the public pages never 500 on a missing table.
/// </summary>
public interface IHomeBlocksReadRepository
{
    Task<HomeStaticBlocksModel> GetHomeBlocksAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerModel>> GetPartnersAsync(CancellationToken cancellationToken = default);
}
