using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface IArtistReadRepository
{
    Task<IReadOnlyList<ArtistSummaryModel>> GetArtistsAsync(bool newestFirst, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会员风采（Vip）列表：按后台维护的 <c>ArtistDisplay</c> 排序并剔除隐藏者，没有记录的按原 Id 倒序排在后面。
    /// <c>ArtistDisplay</c> 表不存在（老库未执行 SQL）时完全回退 <c>GetArtistsAsync(newestFirst: true)</c> 的原顺序。
    /// </summary>
    Task<IReadOnlyList<ArtistSummaryModel>> GetVipArtistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 有艺术家首页内容的艺术家 Id：至少有一件作品，或填了简介 / 职务 / 年表之一。
    /// 其余（留空没设计的）在首页成员卡、会员风采、在售作品里点头像不跳转（href="javascript:;;"）。
    /// </summary>
    Task<IReadOnlySet<int>> GetArtistIdsWithHomepageAsync(CancellationToken cancellationToken = default);

    Task<ArtistDetailModel?> GetArtistByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ArtistProfileSnapshotModel?> GetArtistProfileAsync(int artistId, CancellationToken cancellationToken = default);

    Task<ArtistArchiveSnapshotModel?> GetArtistArchiveAsync(int artistId, CancellationToken cancellationToken = default);

    Task<ArtistArticleSnapshotModel?> GetArtistArticleAsync(int articleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkSummaryModel>> GetArtistWorksAsync(int artistId, CancellationToken cancellationToken = default);
}
