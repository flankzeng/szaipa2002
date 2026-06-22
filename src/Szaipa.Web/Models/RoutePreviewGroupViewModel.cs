namespace Szaipa.Web.Models;

public sealed class RoutePreviewGroupViewModel
{
    public required string Name { get; init; }

    public required IReadOnlyList<RoutePreviewLinkViewModel> Routes { get; init; }
}
