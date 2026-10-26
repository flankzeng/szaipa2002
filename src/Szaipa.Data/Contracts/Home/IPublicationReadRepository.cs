using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface IPublicationReadRepository
{
    Task<IReadOnlyList<PublicationSummaryModel>> GetLatestPublicationsAsync(int count, CancellationToken cancellationToken = default);

    Task<PublicationSummaryModel?> GetPublicationByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PublicationDetailSnapshotModel?> GetPublicationDetailSnapshotAsync(int id, int relatedCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 首页推荐展览（<c>FeaturedExhibition</c> 表，按 SortOrder 升序，含隐藏项）。旧库没有该表时返回空列表而不是抛错，
    /// 调用方据此保留原有硬编码展览块。
    /// </summary>
    Task<IReadOnlyList<FeaturedExhibitionModel>> GetHomeFeaturedAsync(CancellationToken cancellationToken = default);
}
