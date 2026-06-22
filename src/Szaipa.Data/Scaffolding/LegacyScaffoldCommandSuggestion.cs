using Szaipa.Data.Configuration;

namespace Szaipa.Data.Scaffolding;

public sealed class LegacyScaffoldCommandSuggestion
{
    public required LegacyDataSource Source { get; init; }

    public required string ContextName { get; init; }

    public required string AccessMode { get; init; }

    public required bool IsReady { get; init; }

    public required string EnvironmentVariableName { get; init; }

    public required string Command { get; init; }

    public required string Notes { get; init; }
}
