namespace Szaipa.Web.Models;

public sealed class MigrationStubPageViewModel
{
    public required string Title { get; init; }

    public required string LegacyRoute { get; init; }

    public required string TargetRepository { get; init; }

    public required string TargetBatch { get; init; }

    public required IReadOnlyList<string> NextSteps { get; init; }
}
