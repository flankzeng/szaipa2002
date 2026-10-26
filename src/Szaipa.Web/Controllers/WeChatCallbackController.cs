using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;

namespace Szaipa.Web.Controllers;

/// <summary>
/// 公众号「服务器配置」回调 <c>/wechat/callback</c>（<b>默认关闭</b>：<c>WeChat:Callback:Enabled=false</c> 时一律 404）。
/// GET = URL 验证（签名通过原样返回 echostr）；POST = 明文模式事件推送，只处理群发完成事件
/// <c>MASSSENDJOBFINISH</c>：取出 ArticleUrlResult 里的文章链接放入导入队列，由后台 worker 走统一导入器。
/// 签名（Token）校验不通过一律 403，不做任何处理。
/// <para>风险提示：在公众平台启用「服务器配置」会接管消息处理，编辑模式下的自动回复与自定义菜单会失效。</para>
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("wechat/callback")]
public sealed class WeChatCallbackController : Controller
{
    private const int MaxBodyBytes = 256 * 1024;

    private readonly IOptions<WeChatOptions> _options;
    private readonly WeChatImportQueue _queue;
    private readonly IWeChatSourceStatusStore _status;

    public WeChatCallbackController(IOptions<WeChatOptions> options, WeChatImportQueue queue, IWeChatSourceStatusStore status)
    {
        _options = options;
        _queue = queue;
        _status = status;
    }

    [HttpGet("")]
    public IActionResult Verify(string? signature, string? timestamp, string? nonce, string? echostr)
    {
        var callback = _options.Value.Callback;
        if (!callback.IsActive)
        {
            return NotFound();
        }

        return WeChatCallbackProtocol.VerifySignature(callback.Token, signature, timestamp, nonce) && !string.IsNullOrEmpty(echostr)
            ? Content(echostr, "text/plain")
            : StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpPost("")]
    public async Task<IActionResult> Receive(string? signature, string? timestamp, string? nonce, CancellationToken cancellationToken)
    {
        var callback = _options.Value.Callback;
        if (!callback.IsActive)
        {
            return NotFound();
        }

        if (!WeChatCallbackProtocol.VerifySignature(callback.Token, signature, timestamp, nonce))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (Request.ContentLength > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var buffer = new char[MaxBodyBytes + 1];
        using var reader = new StreamReader(Request.Body);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        var urls = WeChatCallbackProtocol.ParseMassSendFinish(new string(buffer, 0, Math.Min(read, MaxBodyBytes)));

        var accepted = 0;
        foreach (var url in urls ?? Array.Empty<string>())
        {
            var normalized = WeChatSourceKey.NormalizeArticleUrl(url);
            if (normalized is not null && _queue.TryEnqueue(normalized))
            {
                accepted++;
            }
        }

        if (urls is { Count: > 0 })
        {
            _status.RecordSuccess(WeChatSources.Callback, $"收到群发完成事件，{accepted} 条文章链接已进入导入队列");
        }

        // 微信要求 5 秒内应答，字符串 success 表示已收到（不重试）。
        return Content("success", "text/plain");
    }
}
