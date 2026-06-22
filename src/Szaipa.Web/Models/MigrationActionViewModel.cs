namespace Szaipa.Web.Models;

public sealed class MigrationActionViewModel
{
    public required string ActionName { get; init; }

    public required string Purpose { get; init; }

    public required IReadOnlyList<string> Queries { get; init; }

    public required IReadOnlyList<string> WriteSideBehaviorsToRemove { get; init; }
}
