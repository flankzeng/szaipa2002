using Szaipa.Data.Configuration;

namespace Szaipa.Data.Scaffolding;

public sealed class LegacyReadBatch
{
    public required string Name { get; init; }

    public required LegacyDataSource Source { get; init; }

    public required IReadOnlyList<string> Entities { get; init; }

    public required IReadOnlyList<string> Controllers { get; init; }

    public required string Goal { get; init; }

    public required IReadOnlyList<LegacyReadActionPlan> Actions { get; init; }
}
