namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 一篇公众号文章的"原始"形态（两条通道——文章链接解析 / 官方 freepublish 接口——都先落成它，再交给同一个导入器）。
/// </summary>
public sealed record WeChatArticle
{
    /// <summary>去重主键，见 <see cref="WeChatSourceKey"/>。</summary>
    public required string SourceKey { get; init; }

    /// <summary>规范化后的文章链接（写入 News.link）。</summary>
    public required string SourceUrl { get; init; }

    public required string Title { get; init; }

    /// <summary>作者；公众号文章常为空，导入时回退为公众号名称。</summary>
    public string? Author { get; init; }

    /// <summary>公众号名称。</summary>
    public string? AccountName { get; init; }

    /// <summary>摘要（→ News.Subtitle）。</summary>
    public string? Digest { get; init; }

    /// <summary>发布时间，北京时间（无时区，与库里其他 News.Date 一致）。取不到时为 null，导入时用当前时间。</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>封面图远程地址。</summary>
    public string? CoverUrl { get; init; }

    /// <summary>正文原始 HTML（页面通道取 #js_content 的内层，接口通道取 news_item.content）。</summary>
    public required string ContentHtml { get; init; }

    /// <summary>官方接口 article_id。</summary>
    public string? ArticleId { get; init; }

    public int ItemIndex { get; init; }

    /// <summary>来源通道：link / freepublish / material / callback（写入 WeChatSyncRecord.Source）。</summary>
    public string Source { get; init; } = WeChatSources.Link;
}

/// <summary>导入一篇文章的结果状态。</summary>
public enum WeChatImportStatus
{
    Created,
    Updated,
    Unchanged,
    Failed,

    /// <summary>
    /// 页面正常但没有可展示的正文（纯视频、转载他号文章的分享卡片等），按设计不导入。
    /// 不算失败：定时同步不会因为它一直报「未完成」。
    /// </summary>
    Skipped
}

/// <summary>导入一篇文章的结果（后台页面逐条展示）。</summary>
public sealed record WeChatImportOutcome(
    string Input,
    WeChatImportStatus Status,
    int? NewsId,
    string? Title,
    string? Message);

/// <summary>来源通道标识（同步记录的 Source、后台"来源状态"的行键）。</summary>
public static class WeChatSources
{
    public const string Link = "link";
    public const string FreePublish = "freepublish";
    public const string Material = "material";
    public const string Callback = "callback";

    public static string DisplayName(string id) => id switch
    {
        Link => "链接导入",
        FreePublish => "发布记录接口（freepublish）",
        Material => "永久图文素材接口（material）",
        Callback => "群发完成事件回调",
        _ => id
    };
}

/// <summary>
/// 接口来源返回的一条文章：只有 <see cref="Url"/> 是必须的——导入时会用它抓页面，从页面脚本取
/// biz/mid/idx 归一到统一去重键；抓页面失败时才退回使用接口自带的 <see cref="ContentHtml"/>。
/// </summary>
public sealed record WeChatDiscoveredArticle(
    string Url,
    string? Title,
    string? Author,
    string? Digest,
    string? CoverUrl,
    string? ContentHtml,
    DateTime? PublishedAt,
    string? ArticleId,
    int ItemIndex);

/// <summary>接口来源的一页（offset 以接口的 item 计，ItemCount 为本页 item 数）。</summary>
public sealed record WeChatSourcePage(int TotalCount, int ItemCount, IReadOnlyList<WeChatDiscoveredArticle> Articles);

/// <summary>单个来源一次同步的结果。</summary>
public sealed record WeChatSourceRunResult(
    string SourceId,
    int Fetched,
    int Created,
    int Updated,
    int Unchanged,
    int Failed,
    string? Error,
    int Skipped = 0);

/// <summary>一次接口同步（所有启用来源）的汇总；任一来源出错不影响其他来源。</summary>
public sealed record WeChatSyncSummary(IReadOnlyList<WeChatSourceRunResult> Sources, string? Error)
{
    public int Fetched => Sources.Sum(s => s.Fetched);
    public int Created => Sources.Sum(s => s.Created);
    public int Updated => Sources.Sum(s => s.Updated);
    public int Unchanged => Sources.Sum(s => s.Unchanged);
    public int Failed => Sources.Sum(s => s.Failed);
    public int Skipped => Sources.Sum(s => s.Skipped);

    public static WeChatSyncSummary Failure(string error) => new(Array.Empty<WeChatSourceRunResult>(), error);
}
