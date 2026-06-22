using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface IPublicationReadRepository
{
    Task<IReadOnlyList<PublicationSummaryModel>> GetLatestPublicationsAsync(int count, CancellationToken cancellationToken = default);

    Task<PublicationSummaryModel?> GetPublicationByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PublicationDetailSnapshotModel?> GetPublicationDetailSnapshotAsync(int id, int relatedCount, CancellationToken cancellationToken = default);
}
