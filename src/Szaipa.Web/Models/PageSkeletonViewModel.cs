namespace Szaipa.Web.Models;

public sealed class PageSkeletonViewModel
{
    public required string Title { get; init; }

    public required string Eyebrow { get; init; }

    public required string Lead { get; init; }

    public required string LegacyRoute { get; init; }

    public required string TargetRepository { get; init; }

    public required string TargetBatch { get; init; }

    public required IReadOnlyList<PageSkeletonSectionViewModel> Sections { get; init; }

    public required IReadOnlyList<PageAssetDependencyViewModel> AssetDependencies { get; init; }

    public required IReadOnlyList<PageSkeletonQueryParameterViewModel> QueryParameters { get; init; }

    public required IReadOnlyList<PageSkeletonGuardrailViewModel> Guardrails { get; init; }

    public required IReadOnlyList<PageSkeletonTargetModelViewModel> TargetReadModels { get; init; }

    public required IReadOnlyList<PageSkeletonRemovedWriteViewModel> RemovedWriteBehaviors { get; init; }

    public PageSkeletonBlueprintViewModel? Blueprint { get; init; }

    public required IReadOnlyList<string> NextSteps { get; init; }
}
