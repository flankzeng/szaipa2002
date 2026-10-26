using Szaipa.Data.Services.WeChat;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>后台「公众号同步」页：同步记录列表 + 链接导入表单 + 本次操作的结果。</summary>
public sealed class WeChatIndexViewModel
{
    public required WeChatSyncListResult List { get; init; }

    /// <summary>官方接口通道是否可用（WeChat:Enabled 且凭据齐全）。</summary>
    public bool ApiConfigured { get; init; }

    /// <summary>是否配置了可写的内容目录（图片落盘需要）。</summary>
    public bool ContentRootReady { get; init; }

    public int? LegacyNewsMaxId { get; init; }

    /// <summary>各来源的启用情况、最近成功时间与最近错误。</summary>
    public IReadOnlyList<WeChatSourceStatusEntry> Sources { get; init; } = Array.Empty<WeChatSourceStatusEntry>();

    /// <summary>用户上次粘贴的链接（导入后回填，方便重试失败项）。</summary>
    public string? ImportInput { get; init; }

    /// <summary>本次链接导入的逐条结果；非导入请求时为 null。</summary>
    public IReadOnlyList<WeChatImportOutcome>? Results { get; init; }
}
