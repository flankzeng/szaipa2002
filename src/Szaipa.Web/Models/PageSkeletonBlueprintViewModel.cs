namespace Szaipa.Web.Models;

public sealed class PageSkeletonBlueprintViewModel
{
    public required string RepositoryName { get; init; }

    public required string MethodName { get; init; }

    public required string Source { get; init; }

    public required string Purpose { get; init; }

    public required IReadOnlyList<string> Entities { get; init; }

    public required IReadOnlyList<string> QuerySteps { get; init; }
}
