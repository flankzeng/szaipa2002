namespace Szaipa.Data.Models.Home;

/// <summary>One row of an exhibition's optional 参展作品目录 (works catalog), rendered on the 重要 skin
/// between the 序 and the on-site photo gallery. Ordered by <c>SortOrder</c> at the query level.</summary>
public sealed class ExhibitionWorkModel
{
    public int Id { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Size { get; init; } = string.Empty;

    public string Medium { get; init; } = string.Empty;

    public string ImagePath { get; init; } = string.Empty;
}
