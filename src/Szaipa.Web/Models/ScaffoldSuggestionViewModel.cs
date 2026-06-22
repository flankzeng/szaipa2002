namespace Szaipa.Web.Models;

public sealed class ScaffoldSuggestionViewModel
{
    public required string Source { get; init; }

    public required string ContextName { get; init; }

    public required string AccessMode { get; init; }

    public required bool IsReady { get; init; }

    public required string EnvironmentVariableName { get; init; }

    public required string Command { get; init; }

    public required string Notes { get; init; }
}
