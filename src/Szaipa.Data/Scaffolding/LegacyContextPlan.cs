using Szaipa.Data.Configuration;

namespace Szaipa.Data.Scaffolding;

public sealed class LegacyContextPlan
{
    public required LegacyDataSource Source { get; init; }

    public required string LegacyContextName { get; init; }

    public required string NewContextName { get; init; }

    public required string OutputDirectory { get; init; }

    public required string Notes { get; init; }
}
