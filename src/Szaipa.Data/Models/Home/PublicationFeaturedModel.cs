namespace Szaipa.Data.Models.Home;

/// <summary>首页推荐展览：展览摘要 + 推荐排序 + 卡片轮播图文件名（空 = 取画廊前几张）。</summary>
public sealed class PublicationFeaturedModel
{
    public required PublicationSummaryModel Publication { get; init; }

    public int FeaturedSort { get; init; }

    /// <summary>Explicit card/carousel image file names under <c>/Content/images/{FolderName}/</c>.</summary>
    public IReadOnlyList<string> ImageNames { get; init; } = Array.Empty<string>();
}
