using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Composition;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Models.Home;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services;

/// <summary>
/// Supplies the data-driven homepage static blocks (核心成员 / 顾问团队 / 在售艺术品) and the About page's
/// 合作伙伴 to the Razor views.
/// </summary>
public interface IHomeBlocksProvider
{
    Task<HomeStaticBlocksModel> GetHomeBlocksAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerModel>> GetPartnersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies the same activation rule as <c>HomeController</c> (read models enabled AND the Szaipa source has a
/// non-disabled connection) before touching the database. When inactive it returns the built-in
/// <see cref="HomeBlocksDefaults"/> without resolving the repository, because constructing the read context
/// throws while the Szaipa source is Disabled. When active, a missing table / empty table is handled inside
/// <see cref="IHomeBlocksReadRepository"/>.
/// </summary>
public sealed class HomeBlocksProvider : IHomeBlocksProvider
{
    private readonly ReadOnlyMigrationOptions _readOnlyMigration;
    private readonly IMigrationWorkspaceDiagnosticsService _diagnostics;
    private readonly IServiceProvider _services;

    public HomeBlocksProvider(
        IOptions<ReadOnlyMigrationOptions> readOnlyMigration,
        IMigrationWorkspaceDiagnosticsService diagnostics,
        IServiceProvider services)
    {
        _readOnlyMigration = readOnlyMigration.Value;
        _diagnostics = diagnostics;
        _services = services;
    }

    public async Task<HomeStaticBlocksModel> GetHomeBlocksAsync(CancellationToken cancellationToken = default)
    {
        if (!ReadModelsActive())
        {
            return new HomeStaticBlocksModel
            {
                CoreMembers = HomeBlocksDefaults.CoreMembers,
                AdvisoryMembers = HomeBlocksDefaults.AdvisoryMembers,
                SaleWorks = HomeBlocksDefaults.SaleWorks
            };
        }

        var repository = _services.GetRequiredService<IHomeBlocksReadRepository>();
        return await repository.GetHomeBlocksAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerModel>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        if (!ReadModelsActive())
        {
            return HomeBlocksDefaults.Partners;
        }

        var repository = _services.GetRequiredService<IHomeBlocksReadRepository>();
        return await repository.GetPartnersAsync(cancellationToken);
    }

    private bool ReadModelsActive()
    {
        if (!_readOnlyMigration.EnableSzaipaReadModels)
        {
            return false;
        }

        var diagnostics = _diagnostics.GetDiagnostics();
        return diagnostics.LegacySources.TryGetValue(nameof(LegacyDataSource.Szaipa), out var source)
            && source.HasConnectionString
            && source.AccessMode != nameof(LegacyAccessMode.Disabled);
    }
}
