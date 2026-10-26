using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Szaipa.Data.Services.WeChat;

/// <summary>公众号文章页面无法解析（文章被删、违规、需要验证、页面结构变化等）。消息可直接展示给后台用户。</summary>
public sealed class WeChatParseException : Exception
{
    public WeChatParseException(string message) : base(message)
    {
    }
}

/// <summary>
/// 解析 <c>https://mp.weixin.qq.com/s/...</c> 公开文章页（只读 GET，不登录）。字段来源：
/// <list type="bullet">
///   <item>标题：og:title → 页面脚本 <c>msg_title</c>；</item>
///   <item>发布时间：脚本 <c>var ct = "unix 秒"</c>（换算为北京时间）→ <c>create_time: 'yyyy-MM-dd HH:mm'</c>；</item>
///   <item>作者/公众号名：meta author / 脚本 <c>author</c> / <c>nickname</c>；</item>
///   <item>摘要：脚本 <c>msg_desc</c> → og:description；封面：og:image → <c>msg_cdn_url</c>；</item>
///   <item>去重键：脚本 <c>biz/mid/idx</c>；正文：<c>#js_content</c> 内层 HTML。</item>
/// </list>
/// </summary>
public static class WeChatArticlePageParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public static WeChatArticle Parse(string html, string requestedUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new WeChatParseException("页面内容为空。");
        }

        var contentHtml = ExtractContent(html);
        if (contentHtml is null)
        {
            throw new WeChatParseException(DescribeMissingContent(html));
        }

        var meta = ReadHeadMeta(html);
        var title = FirstNonEmpty(
            meta.GetValueOrDefault("og:title"),
            ReadJsString(html, "msg_title"));
        title = title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new WeChatParseException("未能解析出文章标题。");
        }

        var biz = ReadJsString(html, "biz");
        var mid = ReadJsString(html, "mid");
        var idx = ReadJsString(html, "idx");
        var normalizedRequest = WeChatSourceKey.NormalizeArticleUrl(requestedUrl);
        var msgLink = WeChatSourceKey.NormalizeArticleUrl(ReadJsString(html, "msg_link"));
        var sourceUrl = msgLink ?? normalizedRequest ?? requestedUrl.Trim();

        string sourceKey;
        var itemIndex = 0;
        if (!string.IsNullOrEmpty(biz) && !string.IsNullOrEmpty(mid) && !string.IsNullOrEmpty(idx))
        {
            sourceKey = WeChatSourceKey.FromIds(biz, mid, idx);
            itemIndex = int.TryParse(idx, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIdx) ? Math.Max(0, parsedIdx - 1) : 0;
        }
        else
        {
            sourceKey = WeChatSourceKey.TryFromLongUrl(requestedUrl) ?? WeChatSourceKey.FromUrl(sourceUrl);
        }

        var author = FirstNonEmpty(
            meta.GetValueOrDefault("og:article:author"),
            meta.GetValueOrDefault("author"),
            ReadJsString(html, "author"));
        var account = FirstNonEmpty(ReadJsString(html, "nickname"), ReadElementText(html, "js_name"));
        var digest = FirstNonEmpty(
            ReadJsString(html, "msg_desc"),
            meta.GetValueOrDefault("og:description"),
            meta.GetValueOrDefault("description"));
        var cover = FirstNonEmpty(meta.GetValueOrDefault("og:image"), ReadJsString(html, "msg_cdn_url"));

        return new WeChatArticle
        {
            SourceKey = sourceKey,
            SourceUrl = sourceUrl,
            Title = title,
            Author = author?.Trim(),
            AccountName = account?.Trim(),
            Digest = digest?.Trim(),
            PublishedAt = ReadPublishedAt(html),
            CoverUrl = cover?.Trim(),
            ContentHtml = contentHtml,
            ItemIndex = itemIndex,
            Source = "link"
        };
    }

    /// <summary>北京时间（UTC+8），与库里其余 News.Date 的"本地时间"口径一致，且不依赖服务器时区。</summary>
    public static DateTime FromUnixSecondsToBeijing(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.AddHours(8);

    private static DateTime? ReadPublishedAt(string html)
    {
        var ct = ReadJsString(html, "ct");
        if (long.TryParse(ct, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 946684800)
        {
            return FromUnixSecondsToBeijing(seconds);
        }

        var match = Regex.Match(html, @"create_time:\s*'(\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2}(?::\d{2})?)?)'", RegexOptions.None, RegexTimeout);
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ExtractContent(string html)
    {
        var marker = html.IndexOf("id=\"js_content\"", StringComparison.Ordinal);
        if (marker < 0)
        {
            return null;
        }

        var start = html.LastIndexOf("<div", marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        // 只需要这个 div 自身；后面的脚本整块当原始文本跳过，解析代价可接受。
        var root = MiniHtml.Parse(html[start..]);
        var container = root.ChildElements.FirstOrDefault();
        return container is null ? null : MiniHtml.SerializeChildren(container).Trim();
    }

    private static string DescribeMissingContent(string html)
    {
        if (html.Contains("已被发布者删除", StringComparison.Ordinal))
        {
            return "该文章已被发布者删除。";
        }

        if (html.Contains("违规", StringComparison.Ordinal) && html.Contains("无法查看", StringComparison.Ordinal))
        {
            return "该文章因违规无法查看。";
        }

        if (html.Contains("环境异常", StringComparison.Ordinal) || html.Contains("完成验证", StringComparison.Ordinal))
        {
            return "微信要求人机验证（环境异常），请稍后重试或改用官方接口同步。";
        }

        if (html.Contains("已停止访问", StringComparison.Ordinal) || html.Contains("被投诉", StringComparison.Ordinal))
        {
            return "该文章已被停止访问。";
        }

        return "未找到文章正文（页面结构可能已变化，或链接不是公开文章）。";
    }

    // ---------------------------------------------------------------- 脚本变量 / meta / 元素文本

    /// <summary>读取页面脚本里的 <c>var name = "..."</c>（兼容 htmlDecode("...") 与 '...'.html(false) 包装），返回解码后的文本。</summary>
    internal static string? ReadJsString(string html, string name)
    {
        var pattern = @"var\s+" + Regex.Escape(name) + @"\s*=\s*(?:htmlDecode\(\s*)?(?<q>[""'])(?<v>(?:\\.|(?!\k<q>).)*)\k<q>";
        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.None, RegexTimeout))
        {
            var value = match.Groups["v"].Value;
            if (value.Length == 0)
            {
                continue;
            }

            return WebUtility.HtmlDecode(UnescapeJs(value));
        }

        return null;
    }

    internal static string UnescapeJs(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '\\' || i + 1 >= value.Length)
            {
                builder.Append(c);
                continue;
            }

            var next = value[++i];
            switch (next)
            {
                case 'x' when i + 2 < value.Length && TryHex(value, i + 1, 2, out var x):
                    builder.Append((char)x);
                    i += 2;
                    break;
                case 'u' when i + 4 < value.Length && TryHex(value, i + 1, 4, out var u):
                    builder.Append((char)u);
                    i += 4;
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                default:
                    builder.Append(next);
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryHex(string value, int start, int length, out int result) =>
        int.TryParse(value.AsSpan(start, length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);

    private static Dictionary<string, string> ReadHeadMeta(string html)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var head = headEnd > 0 ? html[..headEnd] : html[..Math.Min(html.Length, 200_000)];
        foreach (var element in MiniHtml.Descendants(MiniHtml.Parse(head)))
        {
            if (element.Name != "meta")
            {
                continue;
            }

            var key = element.GetAttribute("property") ?? element.GetAttribute("name");
            var content = element.GetAttribute("content");
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrWhiteSpace(content))
            {
                result.TryAdd(key, content.Trim());
            }
        }

        return result;
    }

    private static string? ReadElementText(string html, string id)
    {
        var match = Regex.Match(html, "id=\"" + Regex.Escape(id) + "\"[^>]*>(?<t>[^<]{1,100})<", RegexOptions.None, RegexTimeout);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["t"].Value).Trim() : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
