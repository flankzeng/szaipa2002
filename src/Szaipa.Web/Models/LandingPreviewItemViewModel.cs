namespace Szaipa.Web.Models;

public sealed class LandingPreviewItemViewModel
{
    public required string Label { get; init; }

    public string Marker { get; init; } = string.Empty;

    public string Meta { get; init; } = string.Empty;

    public string MetaAccent { get; init; } = string.Empty;

    public string Subline { get; init; } = string.Empty;

    public string CallToAction { get; init; } = string.Empty;

    public string SecondaryAction { get; init; } = string.Empty;

    public string TertiaryAction { get; init; } = string.Empty;

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public required bool IsFeatured { get; init; }

    public required LandingPreviewItemKind Kind { get; init; }
}
