namespace Szaipa.Web.Models;

public sealed class LandingPreviewViewModel
{
    public required string Eyebrow { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<LandingPreviewSectionViewModel> Sections { get; init; }
}
