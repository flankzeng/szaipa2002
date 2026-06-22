namespace Szaipa.Data.Scaffolding;

public sealed class LegacyRepositoryPlan
{
    public required string RepositoryName { get; init; }

    public required string Source { get; init; }

    public required IReadOnlyList<LegacyRepositoryMethodPlan> Methods { get; init; }
}
