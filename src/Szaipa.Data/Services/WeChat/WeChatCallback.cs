using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 群发完成事件回调（<c>/wechat/callback</c>）的协议部分：签名校验与 MASSSENDJOBFINISH 事件解析。纯函数，便于单测。
/// 仅支持<b>明文模式</b>：URL 验证（GET）与事件推送（POST，XML）都用 <c>sha1(sort(token, timestamp, nonce))</c> 验签。
/// </summary>
public static class WeChatCallbackProtocol
{
    /// <summary>校验 signature = sha1(字典序排序后的 token+timestamp+nonce)；常量时间比较。</summary>
    public static bool VerifySignature(string token, string? signature, string? timestamp, string? nonce)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        var parts = new[] { token, timestamp, nonce };
        Array.Sort(parts, StringComparer.Ordinal);
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(parts)))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hash),
            Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    /// <summary>
    /// 解析 POST 的 XML。是 <c>MASSSENDJOBFINISH</c> 事件时返回 <c>ArticleUrlResult</c> 里的全部文章 URL（可能为空列表）；
    /// 不是该事件、是加密报文或 XML 非法返回 null。禁用 DTD/外部实体，防 XXE。
    /// </summary>
    public static IReadOnlyList<string>? ParseMassSendFinish(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml) || xml.Length > 256 * 1024)
        {
            return null;
        }

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 256 * 1024 };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root is null || root.Element("Encrypt") is not null)
            {
                return null;
            }

            if (!string.Equals(root.Element("MsgType")?.Value.Trim(), "event", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(root.Element("Event")?.Value.Trim(), "MASSSENDJOBFINISH", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return root.Descendants("ArticleUrl")
                .Select(e => e.Value.Trim())
                .Where(u => u.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

/// <summary>回调收到的文章链接的导入队列（有界，满了丢弃并让微信重试/靠其他来源补齐）。</summary>
public sealed class WeChatImportQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryEnqueue(string url) => _channel.Writer.TryWrite(url);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// 消费 <see cref="WeChatImportQueue"/>：对每条文章链接走统一导入器（来源标记 callback）。
/// 刚群发完页面可能还没就绪，失败时间隔 1 分钟 / 5 分钟各重试一次。仅当回调启用且后台写库已配置时运行。
/// </summary>
public sealed class WeChatCallbackImportWorker : BackgroundService
{
    private static readonly AdminActor SystemActor = new(0, "公众号回调");
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };

    private readonly WeChatImportQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WeChatOptions> _options;
    private readonly IOptions<AdminWriteOptions> _adminOptions;
    private readonly IWeChatSourceStatusStore _status;
    private readonly ILogger<WeChatCallbackImportWorker> _logger;

    public WeChatCallbackImportWorker(
        WeChatImportQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WeChatOptions> options,
        IOptions<AdminWriteOptions> adminOptions,
        IWeChatSourceStatusStore status,
        ILogger<WeChatCallbackImportWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _adminOptions = adminOptions;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Callback.IsActive || !_adminOptions.Value.IsConfigured)
        {
            return;
        }

        try
        {
            await foreach (var url in _queue.ReadAllAsync(stoppingToken))
            {
                await ImportWithRetryAsync(url, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    private async Task ImportWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        string? lastMessage = null;
        foreach (var delay in RetryDelays)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWeChatSyncService>();
                var results = await service.ImportLinksAsync(url, SystemActor, force: false, cancellationToken, WeChatSources.Callback);
                if (results.Count > 0 && results.All(r => r.Status != WeChatImportStatus.Failed))
                {
                    return;
                }

                lastMessage = results.FirstOrDefault(r => r.Message is not null)?.Message;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "群发回调导入出错，稍后重试。");
                lastMessage = "导入过程出错：" + exception.GetType().Name;
            }
        }

        _status.RecordFailure(WeChatSources.Callback, lastMessage ?? "导入失败");
        _logger.LogWarning("群发回调文章多次导入失败：{Message}", lastMessage);
    }
}
