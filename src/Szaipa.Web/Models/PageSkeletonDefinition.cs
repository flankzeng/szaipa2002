namespace Szaipa.Web.Models;

public sealed class PageSkeletonDefinition
{
    public required string Title { get; init; }

    public required string Eyebrow { get; init; }

    public required string Lead { get; init; }

    public required string LegacyRoute { get; init; }

    public required string TargetRepository { get; init; }

    public required string TargetBatch { get; init; }

    public required IReadOnlyList<PageSkeletonSectionViewModel> Sections { get; init; }

    public required IReadOnlyList<PageAssetDependencyViewModel> AssetDependencies { get; init; }

    public IReadOnlyList<PageSkeletonQueryParameterViewModel> QueryParameters { get; init; } =
        Array.Empty<PageSkeletonQueryParameterViewModel>();

    public IReadOnlyList<PageSkeletonGuardrailViewModel> Guardrails { get; init; } =
        Array.Empty<PageSkeletonGuardrailViewModel>();

    public IReadOnlyList<PageSkeletonTargetModelViewModel> TargetReadModels { get; init; } =
        Array.Empty<PageSkeletonTargetModelViewModel>();

    public IReadOnlyList<PageSkeletonRemovedWriteViewModel> RemovedWriteBehaviors { get; init; } =
        Array.Empty<PageSkeletonRemovedWriteViewModel>();

    public PageSkeletonBlueprintViewModel? Blueprint { get; init; }

    public required IReadOnlyList<string> NextSteps { get; init; }
}
