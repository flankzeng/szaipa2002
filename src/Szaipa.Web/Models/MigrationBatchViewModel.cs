namespace Szaipa.Web.Models;

public sealed class MigrationBatchViewModel
{
    public required string Name { get; init; }

    public required string Source { get; init; }

    public required string Goal { get; init; }

    public required IReadOnlyList<string> Entities { get; init; }

    public required IReadOnlyList<MigrationActionViewModel> Actions { get; init; }
}
