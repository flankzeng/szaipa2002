namespace Szaipa.Web.Models;

public sealed class PageAssetDependencyViewModel
{
    public required string SourcePath { get; init; }

    public required string TargetPath { get; init; }

    public required string Reason { get; init; }
}
