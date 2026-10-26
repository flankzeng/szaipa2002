using System.Net;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 文章去重键与链接规范化。链接导入与官方接口两个通道必须能对上同一篇文章，所以键优先取
/// <c>biz + mid + idx</c>（页面脚本变量与接口返回的长链接里都有），取不到再退化。
/// </summary>
public static class WeChatSourceKey
{
    public const string ArticleHost = "mp.weixin.qq.com";

    public static string FromIds(string biz, string mid, string idx) => $"mp:{biz}:{mid}:{idx}";

    public static string FromApi(string? articleId, int itemIndex) => $"api:{articleId}:{itemIndex}";

    public static string FromUrl(string normalizedUrl) => "url:" + normalizedUrl;

    /// <summary>从长链接 <c>/s?__biz=..&amp;mid=..&amp;idx=..</c> 提取 biz/mid/idx；缺任何一个返回 null。</summary>
    public static string? TryFromLongUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Replace("&amp;", "&", StringComparison.Ordinal), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = ParseQuery(uri.Query);
        if (query.TryGetValue("__biz", out var biz) && query.TryGetValue("mid", out var mid) && query.TryGetValue("idx", out var idx)
            && biz.Length > 0 && mid.Length > 0 && idx.Length > 0)
        {
            return FromIds(biz, mid, idx);
        }

        return null;
    }

    /// <summary>
    /// 规范化并校验一条用户粘贴的链接：必须是 https/http 的 mp.weixin.qq.com 文章链接；统一 https，
    /// 去掉 fragment 与会话/追踪参数（scene/token/lang 等），保留 <c>/s/xxx</c> 短链或 <c>__biz/mid/idx/sn</c>，
    /// 长链接若带 <c>chksm</c> 则一并保留——微信对缺 chksm 的长链接会 302 到人机验证页（wappoc_appmsgcaptcha），
    /// 带上原样的 chksm 才能正常取到正文。去重不依赖本 URL，而是 <see cref="TryFromLongUrl"/> / 页面解析出的 biz+mid+idx。
    /// 失败返回 null（含 SSRF 防护：任何其他域名一律拒绝）。
    /// </summary>
    public static string? NormalizeArticleUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var text = input.Trim().Replace("&amp;", "&", StringComparison.Ordinal);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.Equals(uri.Host, ArticleHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        var path = uri.AbsolutePath;
        if (path.StartsWith("/s/", StringComparison.Ordinal) && path.Length > 3)
        {
            return $"https://{ArticleHost}{path}";
        }

        if (path == "/s")
        {
            var query = ParseQuery(uri.Query);
            if (query.TryGetValue("__biz", out var biz) && query.TryGetValue("mid", out var mid) &&
                query.TryGetValue("idx", out var idx) && query.TryGetValue("sn", out var sn))
            {
                var url = $"https://{ArticleHost}/s?__biz={Uri.EscapeDataString(biz)}&mid={Uri.EscapeDataString(mid)}&idx={Uri.EscapeDataString(idx)}&sn={Uri.EscapeDataString(sn)}";
                return query.TryGetValue("chksm", out var chksm) && chksm.Length > 0
                    ? $"{url}&chksm={Uri.EscapeDataString(chksm)}"
                    : url;
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = WebUtility.UrlDecode(eq < 0 ? part : part[..eq]);
            var value = eq < 0 ? string.Empty : WebUtility.UrlDecode(part[(eq + 1)..]);
            result.TryAdd(key, value);
        }

        return result;
    }
}
