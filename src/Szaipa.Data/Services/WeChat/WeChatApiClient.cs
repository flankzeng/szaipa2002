using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>官方接口调用失败（凭据无效、IP 不在白名单、接口未开通、频率限制等）。消息可直接展示，不含 AppSecret/access_token。</summary>
public sealed class WeChatApiException : Exception
{
    public WeChatApiException(string message, int? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }

    public int? ErrorCode { get; }
}

/// <summary>永久素材数量（material/get_materialcount）。</summary>
public sealed record WeChatMaterialCount(int Voice, int Video, int Image, int News);

/// <summary>官方接口客户端（只读）。</summary>
public interface IWeChatApiClient
{
    /// <summary>仅取 token 验证 AppId/AppSecret/IP 白名单（<c>wechat-probe</c> 用）。</summary>
    Task VerifyCredentialsAsync(CancellationToken cancellationToken);

    /// <summary>发布记录 <c>freepublish/batchget</c> 一页（新→旧，count≤20，offset 以 item 计）。</summary>
    Task<WeChatSourcePage> GetPublishedPageAsync(int offset, int count, CancellationToken cancellationToken);

    /// <summary>永久图文素材 <c>material/batchget_material?type=news</c> 一页（count≤20）。</summary>
    Task<WeChatSourcePage> GetMaterialNewsPageAsync(int offset, int count, CancellationToken cancellationToken);

    /// <summary><c>material/get_materialcount</c>。</summary>
    Task<WeChatMaterialCount> GetMaterialCountAsync(CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IWeChatApiClient"/> 实现：
/// <list type="number">
///   <item><c>POST /cgi-bin/stable_token</c> 取 access_token，进程内缓存到过期前 5 分钟（不强制刷新，避免互相顶掉；
///         日额度 2000 次，所以每次同步只会用到 1 次取 token + 每页 1 次列表调用）；</item>
///   <item>token 失效类错误（40001/40014/42001）自动强刷并重试一次；</item>
///   <item>错误码转成后台可读提示（40164 IP 未加白名单、48001/50001 接口未开通…），按调用点区分，互不影响。</item>
/// </list>
/// 经 <see cref="IHttpClientFactory"/> 的命名客户端 <see cref="ClientName"/> 访问，测试里可替换 handler。
/// </summary>
public sealed class WeChatApiClient : IWeChatApiClient
{
    public const string ClientName = "wechat-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<WeChatOptions> _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;

    public WeChatApiClient(IHttpClientFactory httpClientFactory, IOptions<WeChatOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task VerifyCredentialsAsync(CancellationToken cancellationToken) =>
        await GetTokenAsync(forceRefresh: false, cancellationToken);

    public async Task<WeChatSourcePage> GetPublishedPageAsync(int offset, int count, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["offset"] = Math.Max(0, offset), ["count"] = Math.Clamp(count, 1, 20), ["no_content"] = 0 };
        var json = await CallAsync("/cgi-bin/freepublish/batchget", body, cancellationToken);
        return ParsePage(json);
    }

    public async Task<WeChatSourcePage> GetMaterialNewsPageAsync(int offset, int count, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["type"] = "news", ["offset"] = Math.Max(0, offset), ["count"] = Math.Clamp(count, 1, 20) };
        var json = await CallAsync("/cgi-bin/material/batchget_material", body, cancellationToken);
        return ParsePage(json);
    }

    public async Task<WeChatMaterialCount> GetMaterialCountAsync(CancellationToken cancellationToken)
    {
        var json = await CallAsync("/cgi-bin/material/get_materialcount", body: null, cancellationToken);
        return new WeChatMaterialCount(
            json["voice_count"]?.GetValue<int>() ?? 0,
            json["video_count"]?.GetValue<int>() ?? 0,
            json["image_count"]?.GetValue<int>() ?? 0,
            json["news_count"]?.GetValue<int>() ?? 0);
    }

    /// <summary>带 access_token 调用；token 失效时强刷重试一次；errcode≠0 抛 <see cref="WeChatApiException"/>。</summary>
    private async Task<JsonNode> CallAsync(string path, JsonNode? body, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(forceRefresh: attempt > 0, cancellationToken);
            var url = $"{path}?access_token={Uri.EscapeDataString(token)}";
            var json = body is null
                ? await SendAsync(HttpMethod.Get, url, null, cancellationToken)
                : await SendAsync(HttpMethod.Post, url, body, cancellationToken);

            var errorCode = json["errcode"]?.GetValue<int>() ?? 0;
            if ((errorCode is 40001 or 40014 or 42001) && attempt == 0)
            {
                continue; // token 失效：强制刷新后重试一次
            }

            if (errorCode != 0)
            {
                throw new WeChatApiException(DescribeError(errorCode, json["errmsg"]?.GetValue<string>()), errorCode);
            }

            return json;
        }
    }

    /// <summary>freepublish 与 material 两个接口的返回结构相同：item[].content.news_item[]。</summary>
    internal static WeChatSourcePage ParsePage(JsonNode json)
    {
        var total = json["total_count"]?.GetValue<int>() ?? 0;
        var itemCount = json["item_count"]?.GetValue<int>() ?? 0;
        var articles = new List<WeChatDiscoveredArticle>();

        foreach (var item in json["item"]?.AsArray() ?? new JsonArray())
        {
            if (item is null)
            {
                continue;
            }

            var articleId = item["article_id"]?.GetValue<string>() ?? item["media_id"]?.GetValue<string>();
            var updateTime = item["update_time"]?.GetValue<long>();
            var content = item["content"];
            var createTime = content?["create_time"]?.GetValue<long>();
            var publishedSeconds = updateTime ?? createTime;

            var index = 0;
            foreach (var news in content?["news_item"]?.AsArray() ?? new JsonArray())
            {
                var itemIndex = index++;
                if (news is null || news["is_deleted"]?.GetValue<bool>() == true)
                {
                    continue;
                }

                var url = news["url"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue; // 从未发出过的草稿式素材没有 url
                }

                articles.Add(new WeChatDiscoveredArticle(
                    url.Trim(),
                    news["title"]?.GetValue<string>()?.Trim(),
                    news["author"]?.GetValue<string>(),
                    news["digest"]?.GetValue<string>(),
                    news["thumb_url"]?.GetValue<string>(),
                    news["content"]?.GetValue<string>(),
                    publishedSeconds is > 946684800 ? WeChatArticlePageParser.FromUnixSecondsToBeijing(publishedSeconds.Value) : null,
                    articleId,
                    itemIndex));
            }
        }

        return new WeChatSourcePage(total, itemCount, articles);
    }

    private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _token is not null && DateTime.UtcNow < _tokenExpiresUtc)
            {
                return _token;
            }

            var options = _options.Value;
            var body = new JsonObject
            {
                ["grant_type"] = "client_credential",
                ["appid"] = options.AppId,
                ["secret"] = options.AppSecret,
                ["force_refresh"] = forceRefresh
            };
            var json = await SendAsync(HttpMethod.Post, "/cgi-bin/stable_token", body, cancellationToken);

            var errorCode = json["errcode"]?.GetValue<int>() ?? 0;
            var token = json["access_token"]?.GetValue<string>();
            if (errorCode != 0 || string.IsNullOrEmpty(token))
            {
                throw new WeChatApiException(DescribeError(errorCode, json["errmsg"]?.GetValue<string>()), errorCode == 0 ? null : errorCode);
            }

            var expiresIn = json["expires_in"]?.GetValue<int>() ?? 7200;
            _token = token;
            _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 300));
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string pathAndQuery, JsonNode? body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        var baseUrl = _options.Value.ApiBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(method, baseUrl + pathAndQuery);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // 不带出请求 URL（含 access_token）。
            throw new WeChatApiException("无法连接微信接口：" + exception.GetType().Name);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new WeChatApiException($"微信接口返回 HTTP {(int)response.StatusCode}。");
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                return JsonNode.Parse(text) ?? throw new WeChatApiException("微信接口返回了空响应。");
            }
            catch (JsonException)
            {
                throw new WeChatApiException("微信接口返回了无法解析的内容。");
            }
        }
    }

    /// <summary>微信 errcode → 后台可读提示。</summary>
    public static string DescribeError(int errorCode, string? message) => errorCode switch
    {
        40164 => "服务器出口 IP 不在公众号后台的 IP 白名单内（errcode 40164）。请在公众号后台「基础配置 → IP 白名单」添加。",
        40013 or 40125 or 40001 => $"AppId 或 AppSecret 无效（errcode {errorCode}）。",
        48001 => "该接口未开通（errcode 48001：未认证的公众号没有此接口权限）。",
        50001 => "该接口未开通（errcode 50001：用户未授权该 api，通常是账号类型/认证不满足）。",
        48002 => "该接口被禁用（errcode 48002：粉丝拒收消息或账号未开通接口能力）。",
        45009 => "接口调用次数达到当日上限（errcode 45009，日额度 2000 次），请明天再试。",
        45011 => "接口调用过于频繁（errcode 45011），请稍后再试。",
        40014 or 42001 => $"access_token 无效或已过期（errcode {errorCode}）。",
        _ => string.Format(CultureInfo.InvariantCulture, "微信接口错误 errcode {0}：{1}", errorCode, message)
    };
}
