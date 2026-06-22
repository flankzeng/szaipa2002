using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Models;

namespace Szaipa.Web.Services;

public sealed class MigrationWorkspaceDiagnosticsService : IMigrationWorkspaceDiagnosticsService
{
    private readonly ILegacyConnectionPolicy _legacyConnectionPolicy;
    private readonly ILegacyScaffoldCommandService _legacyScaffoldCommandService;
    private readonly ReadOnlyMigrationOptions _readOnlyMigration;

    public MigrationWorkspaceDiagnosticsService(
        ILegacyConnectionPolicy legacyConnectionPolicy,
        ILegacyScaffoldCommandService legacyScaffoldCommandService,
        IOptions<ReadOnlyMigrationOptions> readOnlyMigration)
    {
        _legacyConnectionPolicy = legacyConnectionPolicy;
        _legacyScaffoldCommandService = legacyScaffoldCommandService;
        _readOnlyMigration = readOnlyMigration.Value;
    }

    public MigrationWorkspaceDiagnosticsModel GetDiagnostics()
    {
        var sourceOptions = _legacyConnectionPolicy.GetConfiguredSources();
        var sourceDiagnostics = _legacyConnectionPolicy.GetSourceDiagnostics();
        var scaffoldReadiness = _legacyScaffoldCommandService.GetSuggestions()
            .ToDictionary(item => item.Source.ToString(), item => item.IsReady);

        var legacySources = sourceOptions.ToDictionary(
            item => item.Key.ToString(),
            item =>
            {
                var key = item.Key;
                var diagnostic = sourceDiagnostics[key];

                return new LegacySourceStateModel
                {
                    AccessMode = item.Value.AccessMode.ToString(),
                    ConnectionStringSource = diagnostic.ConnectionStringSource,
                    HasConnectionString = diagnostic.HasConnectionString,
                    ScaffoldReady = scaffoldReadiness.GetValueOrDefault(key.ToString(), false)
                };
            });

        return new MigrationWorkspaceDiagnosticsModel
        {
            HasReadWriteLegacySource = sourceOptions.Any(item => item.Value.AccessMode == LegacyAccessMode.ReadWrite),
            LegacySources = legacySources,
            ReadModelFlags = new Dictionary<string, bool>
            {
                ["EnableSzaipaReadModels"] = _readOnlyMigration.EnableSzaipaReadModels,
                ["EnableTongouReadModels"] = _readOnlyMigration.EnableTongouReadModels
            }
        };
    }
}
