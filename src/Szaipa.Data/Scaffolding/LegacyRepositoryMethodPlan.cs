namespace Szaipa.Data.Scaffolding;

public sealed class LegacyRepositoryMethodPlan
{
    public required string MethodName { get; init; }

    public required string Purpose { get; init; }

    public required IReadOnlyList<string> Entities { get; init; }

    public required IReadOnlyList<string> QuerySteps { get; init; }

    public required IReadOnlyList<LegacyQueryParameterPlan> Parameters { get; init; }

    public required IReadOnlyList<string> TargetModels { get; init; }

    public required IReadOnlyList<string> Guardrails { get; init; }
}
