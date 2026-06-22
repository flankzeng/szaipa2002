namespace Szaipa.Web.Models;

/// <summary>
/// One row of the public read-only link readiness summary. Each flag is computed from the
/// actual route contract (page skeleton definition, repository plan, asset dependencies) so the
/// dashboard reflects the real pre-EF-Core state rather than a hand-maintained status string.
/// </summary>
public sealed class PublicReadRouteReadinessViewModel
{
    public required string Group { get; init; }

    public required string LegacyRoute { get; init; }

    public required string TargetRepositoryMethod { get; init; }

    /// <summary>Route declares a target repository, query parameters, target read models, and guardrails.</summary>
    public required bool ContractComplete { get; init; }

    /// <summary>Route resolves a repository blueprint with a purpose and concrete query steps from <c>LegacyRepositoryPlans</c>.</summary>
    public required bool BlueprintAligned { get; init; }

    /// <summary>Route lists at least one source-to-target asset dependency.</summary>
    public required bool AssetMappingListed { get; init; }

    /// <summary>Route enumerates the legacy write behaviors that must stay removed.</summary>
    public required bool WriteRemovalDefined { get; init; }

    /// <summary>True only when every readiness check passes, meaning scaffold/implementation can begin for this route.</summary>
    public required bool ReadyForScaffold { get; init; }
}
