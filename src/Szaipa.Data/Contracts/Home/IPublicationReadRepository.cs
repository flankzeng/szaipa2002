using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface IPublicationReadRepository
{
    Task<IReadOnlyList<PublicationSummaryModel>> GetLatestPublicationsAsync(int count, CancellationToken cancellationToken = default);

    Task<PublicationSummaryModel?> GetPublicationByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PublicationDetailSnapshotModel?> GetPublicationDetailSnapshotAsync(int id, int relatedCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 首页推荐展览（<c>IsFeatured</c>，按 <c>FeaturedSort</c> 升序）。旧库缺推荐列时返回空列表而不是抛错，
    /// 调用方据此回退到原有硬编码展览块。
    /// </summary>
    Task<IReadOnlyList<PublicationFeaturedModel>> GetHomeFeaturedAsync(CancellationToken cancellationToken = default);
}
