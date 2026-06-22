using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Contracts.Home;

public interface INewsReadRepository
{
    Task<HomePageSnapshotModel> GetHomePageSnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsSummaryModel>> GetLatestNewsAsync(int count, CancellationToken cancellationToken = default);

    Task<NewsDetailModel?> GetNewsByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsSummaryModel>> GetRelatedNewsAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsSummaryModel>> SearchNewsAsync(string keyword, CancellationToken cancellationToken = default);
}
