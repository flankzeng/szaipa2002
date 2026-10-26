using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 把图片字节写入站点 Content 目录的抽象。实现（Szaipa.Web 的 <c>WeChatImageStore</c>）复用
/// <c>IAdminAssetStorage</c> 同一套"子目录净化 + 禁止逃出 ContentRoot"的安全路径逻辑。
/// </summary>
public interface IWeChatImageStore
{
    /// <summary>是否配置了可写的内容目录（<c>LegacyAssets:ContentRoot</c>）。</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 文件已存在则返回其公开 URL（<c>/Content/{subfolder}/{fileName}</c>），否则 null。用来跳过重复下载。
    /// </summary>
    string? TryGetExistingUrl(string subfolder, string fileName);

    /// <summary>写入文件并返回公开 URL；已存在时不覆盖直接返回。失败返回 null（不抛出）。</summary>
    Task<string?> SaveAsync(string subfolder, string fileName, byte[] data, CancellationToken cancellationToken);
}

/// <summary>一张已本地化的图片。</summary>
public sealed record WeChatLocalImage(string Url, string FileName);

/// <summary>
/// 下载公众号图片并落盘：
/// <list type="bullet">
///   <item>正文图 → <c>{ContentRoot}/WeChat/{yyyy}/{MM}/{hash}.{ext}</c>；封面 → <c>{ContentRoot}/newsImg/wx-{hash}.{ext}</c>
///         （与后台 News 模块上传封面同目录，<c>News.CoverPath</c> 只存文件名，前台 <c>/content/newsImg/{CoverPath}</c> 即可显示）；</item>
///   <item>文件名 = 图片 URL 路径部分的 SHA256 前 32 位（不含 <c>?wx_fmt</c> 等查询串），同一张图重复导入直接复用，不重复下载；</item>
///   <item>扩展名由文件头魔数判定（jpg/png/gif/webp/bmp），Content-Type 或 wx_fmt 都不可信；非图片内容一律拒绝；</item>
///   <item>仅下载 <see cref="WeChatOptions.AllowedImageHosts"/> 内域名（由清洗器筛选），大小上限 <see cref="WeChatOptions.MaxImageBytes"/>。</item>
/// </list>
/// HTTP 经 <see cref="IHttpClientFactory"/> 命名客户端 <see cref="ClientName"/>，测试可替换 handler。
/// </summary>
public sealed class WeChatImageLocalizer
{
    public const string ClientName = "wechat-image";
    public const string CoverSubfolder = "newsImg";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWeChatImageStore _store;
    private readonly IOptions<WeChatOptions> _options;
    private readonly ILogger<WeChatImageLocalizer> _logger;

    public WeChatImageLocalizer(
        IHttpClientFactory httpClientFactory,
        IWeChatImageStore store,
        IOptions<WeChatOptions> options,
        ILogger<WeChatImageLocalizer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _store = store;
        _options = options;
        _logger = logger;
    }

    public bool IsAvailable => _store.IsAvailable;

    public static string BodySubfolder(DateTime publishedAt) => $"WeChat/{publishedAt:yyyy}/{publishedAt:MM}";

    /// <summary>正文图：下载（或复用）并返回本地 URL；失败返回 null。</summary>
    public Task<WeChatLocalImage?> LocalizeBodyImageAsync(string remoteUrl, DateTime publishedAt, CancellationToken cancellationToken) =>
        LocalizeAsync(remoteUrl, BodySubfolder(publishedAt), string.Empty, cancellationToken);

    /// <summary>封面：落到 newsImg，文件名加 wx- 前缀；失败返回 null。</summary>
    public Task<WeChatLocalImage?> LocalizeCoverAsync(string remoteUrl, CancellationToken cancellationToken) =>
        LocalizeAsync(remoteUrl, CoverSubfolder, "wx-", cancellationToken);

    private async Task<WeChatLocalImage?> LocalizeAsync(string remoteUrl, string subfolder, string prefix, CancellationToken cancellationToken)
    {
        if (!_store.IsAvailable || !Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var stem = prefix + HashOf(uri);

        // 已下载过（任意受支持扩展名）→ 直接复用。
        foreach (var extension in KnownExtensions)
        {
            var existing = _store.TryGetExistingUrl(subfolder, stem + extension);
            if (existing is not null)
            {
                return new WeChatLocalImage(existing, stem + extension);
            }
        }

        var data = await DownloadAsync(uri, cancellationToken);
        if (data is null)
        {
            return null;
        }

        var ext = SniffExtension(data);
        if (ext is null)
        {
            _logger.LogWarning("公众号图片不是受支持的图片格式，已跳过：{Host}", uri.Host);
            return null;
        }

        var fileName = stem + ext;
        var url = await _store.SaveAsync(subfolder, fileName, data, cancellationToken);
        return url is null ? null : new WeChatLocalImage(url, fileName);
    }

    private static readonly string[] KnownExtensions = { ".jpg", ".png", ".gif", ".webp", ".bmp" };

    private async Task<byte[]?> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        var limit = _options.Value.MaxImageBytes;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(ClientName);
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("公众号图片下载失败 HTTP {Status}：{Host}", (int)response.StatusCode, uri.Host);
                    continue;
                }

                if (response.Content.Headers.ContentLength > limit)
                {
                    _logger.LogWarning("公众号图片超过大小上限，已跳过：{Host}", uri.Host);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > limit)
                    {
                        _logger.LogWarning("公众号图片超过大小上限，已跳过：{Host}", uri.Host);
                        return null;
                    }
                }

                return buffer.ToArray();
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning("公众号图片下载异常 {Type}：{Host}", exception.GetType().Name, uri.Host);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("公众号图片下载超时：{Host}", uri.Host);
            }
        }

        return null;
    }

    /// <summary>URL 路径部分（不含查询串）的 SHA256 前 32 个十六进制字符。</summary>
    public static string HashOf(Uri uri)
    {
        var path = uri.Host.ToLowerInvariant() + uri.AbsolutePath;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..32].ToLowerInvariant();
    }

    /// <summary>按文件头判断图片类型，返回带点的小写扩展名；不是图片返回 null。</summary>
    public static string? SniffExtension(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return null;
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ".jpg";
        }

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ".png";
        }

        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
        {
            return ".gif";
        }

        if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
        {
            return ".webp";
        }

        if (data[0] == 'B' && data[1] == 'M')
        {
            return ".bmp";
        }

        return null;
    }
}
