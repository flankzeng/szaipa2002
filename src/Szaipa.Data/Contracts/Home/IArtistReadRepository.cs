using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface IArtistReadRepository
{
    Task<IReadOnlyList<ArtistSummaryModel>> GetArtistsAsync(bool newestFirst, CancellationToken cancellationToken = default);

    Task<ArtistDetailModel?> GetArtistByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ArtistProfileSnapshotModel?> GetArtistProfileAsync(int artistId, CancellationToken cancellationToken = default);

    Task<ArtistArchiveSnapshotModel?> GetArtistArchiveAsync(int artistId, CancellationToken cancellationToken = default);

    Task<ArtistArticleSnapshotModel?> GetArtistArticleAsync(int articleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkSummaryModel>> GetArtistWorksAsync(int artistId, CancellationToken cancellationToken = default);
}
