namespace Szaipa.Web.Models;

public sealed class LandingPreviewSectionViewModel
{
    public required string AnchorId { get; init; }

    public required LandingPreviewSectionKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string Description { get; init; }

    public required string TargetRoute { get; init; }

    public required IReadOnlyList<string> Dependencies { get; init; }

    public required IReadOnlyList<LandingPreviewItemViewModel> Items { get; init; }
}
