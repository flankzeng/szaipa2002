namespace Szaipa.Web.Models;

public sealed class RepositoryPlanViewModel
{
    public required string RepositoryName { get; init; }

    public required string Source { get; init; }

    public required IReadOnlyList<RepositoryMethodPlanViewModel> Methods { get; init; }
}
