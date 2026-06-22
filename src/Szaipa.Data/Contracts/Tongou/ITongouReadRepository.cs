using Szaipa.Data.Models.Tongou;

namespace Szaipa.Data.Contracts.Tongou;

public interface ITongouReadRepository
{
    Task<TongouArtistModel?> GetArtistByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<TongouArtistProfileSnapshotModel?> GetArtistProfileAsync(int id, CancellationToken cancellationToken = default);

    Task<TongouWorkModel?> GetWorkByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TongouWorkModel>> GetWorksByArtistIdAsync(int artistId, CancellationToken cancellationToken = default);
}
