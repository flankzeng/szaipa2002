namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// 公众号文章同步记录：每篇从微信公众号导入到 <see cref="News"/> 的文章对应一行，用于去重、增量更新、
/// 来源标记，以及「后台隐藏」。<b>新闻本体仍存在 News 表</b>（前台版式零改动），本表只是旁路元数据。
/// <para>
/// 这是一张加性新表（脚本见 <c>docs/sql/2026-10-26-wechat-sync.sql</c>）。所有读写代码都必须容忍它尚不存在：
/// 前台读侧缺表时回退为"显示全部 News"，后台同步页提示"需先执行 SQL"。
/// </para>
/// </summary>
public sealed class WeChatSyncRecord
{
    public int Id { get; set; }

    /// <summary>
    /// 去重主键（唯一索引）。优先 <c>mp:{biz}:{mid}:{idx}</c>（链接导入与官方接口两个通道能对上同一篇），
    /// 取不到时退化为 <c>api:{article_id}:{idx}</c> 或 <c>url:{规范化链接}</c>。
    /// </summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>公众号文章链接（写入 News.link 的同一个值）。</summary>
    public string? SourceUrl { get; set; }

    /// <summary>官方接口的 article_id（链接导入时为空）。</summary>
    public string? ArticleId { get; set; }

    /// <summary>文章在一次群发中的序号（0 = 头条；接口取 news_item 下标，链接导入取页面 idx-1）。</summary>
    public int ItemIndex { get; set; }

    /// <summary>对应的 News.Id。News 被后台删除后本行保留（NewsId 悬空），列表里显示"新闻已删除"。</summary>
    public int NewsId { get; set; }

    /// <summary>清洗后（未本地化图片前）内容 + 标题/摘要/封面的 SHA256 十六进制，用于判断源文章是否被改过。</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>最近一次成功同步（创建/更新）时间。</summary>
    public DateTime SyncedAt { get; set; }

    /// <summary>true 时该文章不出现在前台新闻列表/首页/侧栏推荐；详情 URL 仍可访问。</summary>
    public bool IsHidden { get; set; }

    /// <summary>来源通道：<c>link</c>（链接导入）或 <c>api</c>（官方接口/定时同步）。</summary>
    public string Source { get; set; } = "link";
}
