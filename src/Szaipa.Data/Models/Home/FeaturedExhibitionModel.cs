namespace Szaipa.Data.Models.Home;

/// <summary>首页推荐展览（读侧，自包含卡片数据；字段含义见 <see cref="Contexts.Szaipa.FeaturedExhibition"/>）。</summary>
public sealed class FeaturedExhibitionModel
{
    public int Id { get; init; }

    public int SortOrder { get; init; }

    public bool IsVisible { get; init; }

    public bool ShowInCarousel { get; init; }

    public string Title { get; init; } = string.Empty;

    public string YearText { get; init; } = string.Empty;

    public string LinkUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> InfoLines { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ImageUrls { get; init; } = Array.Empty<string>();

    public string LogoUrl { get; init; } = string.Empty;

    public string CoverUrl { get; init; } = string.Empty;
}
