namespace Szaipa.Web.Models;

public sealed class MigrationDashboardViewModel
{
    public required LandingPreviewViewModel LandingPreview { get; init; }

    public required string TargetFramework { get; init; }

    public required bool UseLegacyDataSources { get; init; }

    public required bool AllowLiveDatabase { get; init; }

    public required bool HasReadWriteLegacySource { get; init; }

    public required string ActiveEnvironment { get; init; }

    public required IReadOnlyDictionary<string, string> LegacySources { get; init; }

    public required IReadOnlyDictionary<string, string> LegacyConnectionSources { get; init; }

    public required IReadOnlyDictionary<string, bool> ScaffoldReadiness { get; init; }

    public required IReadOnlyDictionary<string, bool> ReadModelFlags { get; init; }

    public required IReadOnlyList<ScaffoldSuggestionViewModel> ScaffoldSuggestions { get; init; }

    public required IReadOnlyList<MigrationStepViewModel> Guardrails { get; init; }

    public required IReadOnlyList<MigrationBatchViewModel> Batches { get; init; }

    public required IReadOnlyList<RepositoryPlanViewModel> RepositoryPlans { get; init; }

    public required IReadOnlyList<RoutePreviewGroupViewModel> RoutePreviewGroups { get; init; }

    public required IReadOnlyList<PublicReadRouteReadinessViewModel> PublicReadLinkReadiness { get; init; }

    public required IReadOnlyList<string> Workstreams { get; init; }
}
