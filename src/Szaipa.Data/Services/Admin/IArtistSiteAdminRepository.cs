using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Admin;

/// <summary>艺术家官网总览页（/Staff/Artist/{id}/Site）一次取齐的汇总。</summary>
public sealed class ArtistSiteOverview
{
    public required Artist Artist { get; init; }

    public int WorkCount { get; init; }

    public int NewsCount { get; init; }

    public int FavCount { get; init; }

    public int AuctionCount { get; init; }

    /// <summary>手工维护的 Exhibition 行（旧「展览」子模块）数量。</summary>
    public int ExhibitionRowCount { get; init; }

    /// <summary>勾选关联的展览（Publication）数量；<c>ArtistPublication</c> 表不存在时为 0。</summary>
    public int LinkedPublicationCount { get; init; }

    /// <summary><c>Works.Sort</c> 列是否存在（不存在则作品只能按 Id 顺序，拖拽排序被禁用）。</summary>
    public bool WorksSortSupported { get; init; } = true;

    /// <summary><c>ArtistPublication</c> 表是否存在（不存在则无法关联展览）。</summary>
    public bool ExhibitionLinksSupported { get; init; } = true;

    public bool DefaultExhibitionFlagSupported { get; init; } = true;
}

public sealed class ArtistWorkItem
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public string? Content { get; init; }

    public string? Path { get; init; }

    public int Sort { get; init; }
}

public sealed class ArtistWorkList
{
    public required IReadOnlyList<ArtistWorkItem> Items { get; init; }

    public bool SortSupported { get; init; } = true;
}

/// <summary>批量上传后新增的一件作品（图片已落盘，<see cref="Path"/> 是 works-narrow 目录下的文件名）。</summary>
public sealed record NewArtistWork(string Path, string? Title, string? Content);

public sealed record PublicationOption(int Id, string Title, DateTime? StartDate, string? Location);

public sealed class ArtistExhibitionLinks
{
    /// <summary>已关联展览，按展示顺序。</summary>
    public required IReadOnlyList<PublicationOption> Linked { get; init; }

    /// <summary>true = 不再追加旧版写死的 3 张默认活动图。</summary>
    public bool HideDefaultExhibitions { get; init; }

    public bool Supported { get; init; } = true;
}

/// <summary>
/// 艺术家官网「一站式编辑」的写侧：作品批量新增/排序/删除、展讯（关联展览）勾选排序。
/// 基本信息/头像/横幅/配色仍走 <see cref="IArtistAdminRepository"/>。
/// </summary>
public interface IArtistSiteAdminRepository
{
    Task<bool> SupportsWorksSortAsync(CancellationToken cancellationToken);

    Task<ArtistSiteOverview?> GetOverviewAsync(int artistId, CancellationToken cancellationToken);

    /// <summary>该艺术家的作品，按 Sort 再 Id；缺 Sort 列时按 Id 并标记 <c>SortSupported=false</c>。</summary>
    Task<ArtistWorkList> GetWorksAsync(int artistId, CancellationToken cancellationToken);

    /// <summary>追加作品到作品集末尾，返回新作品 Id（与输入同序）。</summary>
    Task<IReadOnlyList<int>> AddWorksAsync(
        int artistId,
        IReadOnlyList<NewArtistWork> works,
        AdminActor actor,
        CancellationToken cancellationToken);

    Task<bool> UpdateWorkAsync(
        int artistId,
        int workId,
        string? title,
        string? content,
        AdminActor actor,
        CancellationToken cancellationToken);

    Task<bool> DeleteWorkAsync(int artistId, int workId, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>按给定 Id 顺序重排；未出现在列表里的作品保持相对顺序排在最后。</summary>
    Task ReorderWorksAsync(
        int artistId,
        IReadOnlyList<int> orderedWorkIds,
        AdminActor actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicationOption>> GetPublicationOptionsAsync(CancellationToken cancellationToken);

    Task<ArtistExhibitionLinks> GetExhibitionLinksAsync(int artistId, CancellationToken cancellationToken);

    /// <summary>整组替换该艺术家的关联展览（顺序即展示顺序），并保存是否隐藏默认三张。</summary>
    Task<bool> SaveExhibitionLinksAsync(
        int artistId,
        IReadOnlyList<int> orderedPublicationIds,
        bool hideDefaultExhibitions,
        AdminActor actor,
        CancellationToken cancellationToken);
}
