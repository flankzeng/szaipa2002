using System.Text;
using System.Text.RegularExpressions;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>清洗结果。</summary>
/// <param name="Html">可直接写入 News.Content 的白名单 HTML。</param>
/// <param name="ImageUrls">正文里出现的图片原始（远程）地址，去重、保持出现顺序。</param>
/// <param name="PlainText">清洗后正文的纯文本（摘要兜底用）。</param>
public sealed record WeChatCleanedContent(string Html, IReadOnlyList<string> ImageUrls, string PlainText);

/// <summary>
/// 公众号正文清洗：<b>不是照搬</b>。目标是产出"像手录新闻一样"的干净 HTML，吃前台 <c>.article-body</c> 的排版：
/// <list type="number">
///   <item>白名单标签/属性（script、iframe、svg、事件属性、class/id/data-* 一律丢弃，未知标签脱壳保留内容）；</item>
///   <item>剥离内联样式里的字号/字体/颜色/背景/宽度等，只保留对齐、粗体、斜体、下划线、删除线；</item>
///   <item>图片：data-src 优先，只接受允许域名（防 SSRF），可交给 <c>imageMapper</c> 改写为本地地址（映射失败即丢弃，绝不热链）；</item>
///   <item>去公众号专有内容：关注引导、二维码及其配图、阅读原文、点赞在看、名片、小程序卡片、往期推荐（含其后整段尾部）、
///         标题重复、END 分隔符、隐藏元素与空白占位段落；</item>
///   <item>去掉无意义的嵌套壳（无属性的 span / div）。</item>
/// </list>
/// 本类是纯函数（不访问网络/磁盘），规则全部有单测。可在 <see cref="WeChatCleaningOptions"/> 追加噪声/尾部正则。
/// </summary>
public sealed class WeChatContentCleaner
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // 整个丢弃（连同内容）的标签。mp-* / 公众号私有插件另行判断。
    private static readonly HashSet<string> DropWithContent = new(StringComparer.Ordinal)
    {
        "script", "style", "iframe", "object", "embed", "svg", "canvas", "form", "input", "button", "select",
        "textarea", "video", "audio", "source", "track", "link", "meta", "head", "title", "noscript", "template",
        "frame", "frameset", "applet", "base", "map", "area", "math", "mpvoice", "mpprogram", "qqmusic",
        "wx-open-launch-weapp", "wx-open-subscribe"
    };

    // 保留的标签（section/article 等语义块先改名为 div）。
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "p", "div", "span", "br", "hr", "strong", "b", "em", "i", "u", "s", "del", "strike", "sub", "sup", "a",
        "img", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "blockquote", "pre", "code", "table",
        "thead", "tbody", "tfoot", "tr", "td", "th", "caption", "colgroup", "col", "figure", "figcaption"
    };

    private static readonly HashSet<string> RenameToDiv = new(StringComparer.Ordinal)
    {
        "section", "article", "main", "header", "footer", "aside", "nav", "center", "dl", "dt", "dd", "address"
    };

    // 参与"块判定"的容器，用来找叶子块。
    private static readonly HashSet<string> BlockNames = new(StringComparer.Ordinal)
    {
        "p", "div", "li", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "td", "th", "pre", "figure",
        "figcaption", "caption", "table", "ul", "ol", "tr", "thead", "tbody", "tfoot"
    };

    private static readonly HashSet<string> LeafBlockNames = new(StringComparer.Ordinal)
    {
        "p", "div", "li", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "td", "th", "pre", "figcaption"
    };

    // 内置噪声段落规则（对"归一化后的短段落文本"匹配）。
    private static readonly string[] DefaultNoisePatterns =
    {
        // 阅读原文 / 轻触阅读原文
        @"阅读原文",
        // 长按/扫描/识别二维码
        @"(长按|扫描|扫码|扫一扫|识别|微信扫).{0,8}(二维码|二维吗)",
        @"^[\s\W]*(请)?(长按|扫描|扫码|扫一扫|识别|微信扫)[^，。,.]{0,10}(关注|添加|订阅|加入)",
        @"二维码.{0,12}(关注|识别|扫描|添加|订阅|加入)",
        @"^[\s\W]*(识别|扫描|扫码|长按)[\s\W]*$",
        // 点击蓝字关注 / 点击名片关注
        @"(点击|戳|点|按|关注)\s*(上方|下方|上面|下面|顶部|文首|标题下|标题上)?\s*(蓝字|蓝色字|蓝色文字|名片|公众号)\s*.{0,12}$",
        @"点击(上方|下方|上面|下面|顶部).{0,14}关注",
        // 关注我们
        @"(欢迎|扫码|长按|点击|请|记得|一键|立即|快来|快快)?\s*(关注|星标|置顶)\s*(我们|本号|本公众号|公众号|视频号|小程序|订阅号)",
        @"星标.{0,8}(不迷路|置顶)",
        // 点赞 / 在看 / 转发
        @"^[\s\W]*(?:别忘了|记得|欢迎|麻烦|请|喜欢就|如果喜欢|觉得(?:不错|好看))?\s*(?:点个?|给个?|点击|来个?|送个?)?\s*(?:(?:赞|点赞|在看|好看|转发)|(?:分享|收藏|留言)(?=\s*[、，,和与及丨|/·+]))(?:\s*[、，,和与及丨|/·+\s]\s*(?:赞|点赞|在看|好看|转发|分享|收藏|留言))*\s*(?:哦|吧|呀|啦|哈|喔|噢)?[\s!！。.~～]*$",
        // 微信自带提示
        @"预览时标签不可点",
        @"微信扫一扫",
        @"向上滑动看下一个",
        @"喜欢此内容的人还喜欢",
        // 往期推荐标题行本身（其后内容由尾部规则截断）
        @"^[\s\W]*(往期|历史)(推荐|回顾|精彩|文章|热文|好文|内容|链接)",
        // - END -
        @"^[\s\-—–·•*=~_]*END[\s\-—–·•*=~_]*$"
    };

    // 内置尾部截断规则：命中后连同其后全部内容删除（需位于文章后半部分，防止误伤正文里偶然出现的词）。
    private static readonly string[] DefaultTailMarkerPatterns =
    {
        @"^[\s\W]*(往期|历史)(推荐|回顾|精彩|文章|热文|好文|内容|链接)",
        @"^[\s\W]*(相关|延伸|推荐|更多|扩展)阅读",
        @"^[\s\W]*(更多)?精彩推荐",
        @"^[\s\W]*猜你(喜欢|想看)"
    };

    // 与二维码相关的噪声文本：命中时顺带删掉相邻的纯图片块（二维码图本身）。
    private static readonly Regex QrWords = new(@"二维码|扫码|扫一扫|扫描|识别|长按", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled, RegexTimeout);

    private readonly WeChatOptions _options;
    private readonly Regex[] _noise;
    private readonly Regex[] _tail;

    public WeChatContentCleaner(WeChatOptions? options = null)
    {
        _options = options ?? new WeChatOptions();
        _noise = Build(DefaultNoisePatterns, _options.Cleaning.ExtraNoisePatterns);
        _tail = Build(DefaultTailMarkerPatterns, _options.Cleaning.ExtraTailMarkerPatterns);
    }

    private static Regex[] Build(IEnumerable<string> defaults, IEnumerable<string> extras) =>
        defaults.Concat(extras.Where(p => !string.IsNullOrWhiteSpace(p)))
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            .ToArray();

    /// <summary>
    /// 清洗正文片段。<paramref name="imageMapper"/> 为空时 img 保留远程地址（用于算哈希/收集下载清单）；
    /// 非空时由它把远程地址映射为本地 URL，返回 null 表示该图下载失败 → 丢弃。
    /// </summary>
    public WeChatCleanedContent Clean(string rawHtml, string? articleTitle = null, Func<string, string?>? imageMapper = null)
    {
        var root = MiniHtml.Parse(rawHtml ?? string.Empty);
        var images = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        SanitizeChildren(root, images, seen, imageMapper);
        RemoveNoiseAndTail(root);
        // 首轮清洗先收集候选图片；去掉二维码、往期推荐尾部后必须重建清单，
        // 否则已从正文移除的图片仍会被同步服务下载到 Content。
        if (imageMapper is null)
        {
            images.Clear();
            seen.Clear();
            CollectRemainingImages(root, images, seen);
        }
        if (!string.IsNullOrWhiteSpace(articleTitle))
        {
            RemoveLeadingTitle(root, articleTitle);
        }

        Prune(root);
        Simplify(root);
        TrimEdges(root);

        var html = MiniHtml.SerializeChildren(root).Trim();
        var plain = Normalize(MiniHtml.InnerText(root));
        return new WeChatCleanedContent(html, images, plain);
    }

    private void CollectRemainingImages(HtmlElement root, List<string> images, HashSet<string> seen)
    {
        foreach (var image in MiniHtml.Descendants(root).Where(element => element.Name == "img"))
        {
            var remote = ResolveImageUrl(image.GetAttribute("src"));
            if (remote is not null && seen.Add(remote))
            {
                images.Add(remote);
            }
        }
    }

    /// <summary>从 data-src/src 取出可下载的 http(s) 图片地址；不在允许域名内返回 null。</summary>
    public string? ResolveImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var url = raw.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            url = "https:" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        if (!uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        var allowed = _options.AllowedImageHosts.Any(suffix =>
            !string.IsNullOrWhiteSpace(suffix) &&
            (host == suffix.ToLowerInvariant() || host.EndsWith("." + suffix.ToLowerInvariant(), StringComparison.Ordinal)));
        if (!allowed)
        {
            return null;
        }

        // 图床统一走 https，避免混合内容。
        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            var secure = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 };
            return secure.Uri.ToString();
        }

        return uri.ToString();
    }

    // ---------------------------------------------------------------- 1. 白名单 + 属性/样式/图片

    private void SanitizeChildren(HtmlElement parent, List<string> images, HashSet<string> seen, Func<string, string?>? mapper)
    {
        foreach (var child in parent.Children.ToList())
        {
            if (child is not HtmlElement element)
            {
                continue;
            }

            if (ShouldDrop(element))
            {
                MiniHtml.Remove(element);
                continue;
            }

            if (RenameToDiv.Contains(element.Name))
            {
                element.Name = "div";
            }

            if (!Allowed.Contains(element.Name))
            {
                // 未知/不再支持的标签（font、mp 以外的自定义标签等）：脱壳，保留内容。
                SanitizeChildren(element, images, seen, mapper);
                MiniHtml.Unwrap(element);
                continue;
            }

            if (element.Name == "img")
            {
                if (!SanitizeImage(element, images, seen, mapper))
                {
                    MiniHtml.Remove(element);
                }

                continue;
            }

            SanitizeChildren(element, images, seen, mapper);

            if (element.Name == "a" && !SanitizeLink(element))
            {
                MiniHtml.Unwrap(element);
                continue;
            }

            FilterAttributes(element);
        }
    }

    private static bool ShouldDrop(HtmlElement element)
    {
        var name = element.Name;
        if (DropWithContent.Contains(name) || name.StartsWith("mp-", StringComparison.Ordinal))
        {
            return true;
        }

        var cls = element.GetAttribute("class");
        if (cls is not null &&
            (cls.Contains("qr_code", StringComparison.OrdinalIgnoreCase) ||
             cls.Contains("qrcode", StringComparison.OrdinalIgnoreCase) ||
             cls.Contains("rich_media_tool", StringComparison.OrdinalIgnoreCase) ||
             cls.Contains("js_wx_follow", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var pluginName = element.GetAttribute("data-pluginname");
        if (pluginName is "mpvoice" or "insertvideo" or "mpprofile" or "mpcard")
        {
            return true;
        }

        var style = element.GetAttribute("style");
        if (style is not null && Regex.IsMatch(style, @"display\s*:\s*none", RegexOptions.IgnoreCase, RegexTimeout))
        {
            return true;
        }

        return false;
    }

    private bool SanitizeImage(HtmlElement image, List<string> images, HashSet<string> seen, Func<string, string?>? mapper)
    {
        var alt = image.GetAttribute("alt") ?? string.Empty;
        var cls = image.GetAttribute("class") ?? string.Empty;
        if (cls.Contains("qr_code", StringComparison.OrdinalIgnoreCase) ||
            cls.Contains("qrcode", StringComparison.OrdinalIgnoreCase) ||
            alt.Contains("二维码", StringComparison.Ordinal) ||
            alt.Contains("qrcode", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remote = ResolveImageUrl(image.GetAttribute("data-src") ?? image.GetAttribute("src"));
        if (remote is null)
        {
            // 没有可用的 data-src 时，src 可能是占位 data: URI；仍尝试 src。
            remote = ResolveImageUrl(image.GetAttribute("src"));
        }

        if (remote is null)
        {
            return false;
        }

        if (seen.Add(remote))
        {
            images.Add(remote);
        }

        string? finalUrl = remote;
        if (mapper is not null)
        {
            finalUrl = mapper(remote);
            if (finalUrl is null)
            {
                return false;
            }
        }

        image.Attributes.Clear();
        image.Attributes.Add(new KeyValuePair<string, string>("src", finalUrl));
        if (!string.IsNullOrWhiteSpace(alt) && alt.Length <= 200)
        {
            image.Attributes.Add(new KeyValuePair<string, string>("alt", alt.Trim()));
        }

        image.Attributes.Add(new KeyValuePair<string, string>("loading", "lazy"));
        return true;
    }

    private static bool SanitizeLink(HtmlElement link)
    {
        var href = link.GetAttribute("href")?.Trim();
        if (string.IsNullOrEmpty(href))
        {
            return false;
        }

        var safe = href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                   href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
        if (!safe)
        {
            return false;
        }

        link.Attributes.Clear();
        link.Attributes.Add(new KeyValuePair<string, string>("href", href));
        link.Attributes.Add(new KeyValuePair<string, string>("target", "_blank"));
        link.Attributes.Add(new KeyValuePair<string, string>("rel", "noopener noreferrer"));
        return true;
    }

    private static void FilterAttributes(HtmlElement element)
    {
        if (element.Name == "a")
        {
            return; // 已在 SanitizeLink 中重建
        }

        var style = element.GetAttribute("style");
        string? colspan = null, rowspan = null;
        if (element.Name is "td" or "th")
        {
            colspan = DigitsOrNull(element.GetAttribute("colspan"));
            rowspan = DigitsOrNull(element.GetAttribute("rowspan"));
        }

        element.Attributes.Clear();
        if (colspan is not null)
        {
            element.Attributes.Add(new KeyValuePair<string, string>("colspan", colspan));
        }

        if (rowspan is not null)
        {
            element.Attributes.Add(new KeyValuePair<string, string>("rowspan", rowspan));
        }

        var filtered = FilterStyle(style);
        if (filtered.Length > 0)
        {
            element.Attributes.Add(new KeyValuePair<string, string>("style", filtered));
        }
    }

    private static string? DigitsOrNull(string? value) =>
        value is not null && value.Length is > 0 and <= 3 && value.All(char.IsAsciiDigit) && value != "0" ? value : null;

    /// <summary>
    /// 只保留不会破坏站点排版的内联样式：对齐、粗/斜、下划线/删除线。
    /// 字号、字体、颜色、背景、宽高、边距、行高、字距等一律剥离，交给 <c>.article-body</c> 统一排版。
    /// </summary>
    public static string FilterStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return string.Empty;
        }

        var kept = new List<string>();
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var property = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim().ToLowerInvariant();
            switch (property)
            {
                case "text-align" when value is "left" or "center" or "right" or "justify":
                    kept.Add($"text-align: {value}");
                    break;
                case "font-weight" when value is "bold" or "bolder" or "600" or "700" or "800" or "900":
                    kept.Add("font-weight: bold");
                    break;
                case "font-style" when value is "italic" or "oblique":
                    kept.Add("font-style: italic");
                    break;
                case "text-decoration" or "text-decoration-line" when value.StartsWith("underline", StringComparison.Ordinal):
                    kept.Add("text-decoration: underline");
                    break;
                case "text-decoration" or "text-decoration-line" when value.StartsWith("line-through", StringComparison.Ordinal):
                    kept.Add("text-decoration: line-through");
                    break;
            }
        }

        return string.Join("; ", kept.Distinct());
    }

    // ---------------------------------------------------------------- 2. 噪声段落 + 尾部截断

    private sealed class LeafBlock
    {
        public required HtmlElement Element { get; init; }
        public required string Text { get; init; }
        public required bool HasImage { get; init; }
    }

    private List<LeafBlock> CollectLeafBlocks(HtmlElement root)
    {
        var result = new List<LeafBlock>();
        foreach (var element in MiniHtml.Descendants(root))
        {
            if (!LeafBlockNames.Contains(element.Name))
            {
                continue;
            }

            if (MiniHtml.Descendants(element).Any(d => BlockNames.Contains(d.Name)))
            {
                continue;
            }

            result.Add(new LeafBlock
            {
                Element = element,
                Text = Normalize(MiniHtml.InnerText(element)),
                HasImage = MiniHtml.Descendants(element).Any(d => d.Name == "img")
            });
        }

        return result;
    }

    private void RemoveNoiseAndTail(HtmlElement root)
    {
        // 先做尾部截断，再在剩余内容里删噪声段落。
        var blocks = CollectLeafBlocks(root);
        var meaningful = blocks.Where(b => b.Text.Length > 0 || b.HasImage).ToList();
        for (var i = 0; i < meaningful.Count; i++)
        {
            var block = meaningful[i];
            if (block.Text.Length is 0 or > 40 || !_tail.Any(r => r.IsMatch(block.Text)))
            {
                continue;
            }

            // 只信任位于文章后半部分的标记（>= 40% 位置），避免正文开头偶然出现"相关阅读"之类的词。
            if (i >= meaningful.Count * 0.4)
            {
                TruncateFrom(block.Element);
                break;
            }
        }

        blocks = CollectLeafBlocks(root);
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Text.Length is 0 or > 80 || !_noise.Any(r => r.IsMatch(block.Text)))
            {
                continue;
            }

            if (QrWords.IsMatch(block.Text))
            {
                // 二维码文案旁边的纯图片块就是二维码本身，一并删除。
                RemoveAdjacentImageOnlyBlocks(block.Element);
            }

            MiniHtml.Remove(block.Element);
        }
    }

    /// <summary>删除 <paramref name="marker"/> 及其在文档顺序上之后的全部内容。</summary>
    private static void TruncateFrom(HtmlElement marker)
    {
        HtmlNode current = marker;
        while (current.Parent is { } parent)
        {
            var index = parent.Children.IndexOf(current);
            for (var j = parent.Children.Count - 1; j > index; j--)
            {
                parent.Children[j].Parent = null;
                parent.Children.RemoveAt(j);
            }

            current = parent;
        }

        MiniHtml.Remove(marker);
    }

    private static void RemoveAdjacentImageOnlyBlocks(HtmlElement block)
    {
        var parent = block.Parent;
        if (parent is null)
        {
            return;
        }

        var siblings = parent.Children;
        var index = siblings.IndexOf(block);
        for (var j = index - 1; j >= 0; j--)
        {
            if (!TryRemoveImageOnlySibling(siblings, j))
            {
                break;
            }
        }

        index = siblings.IndexOf(block);
        while (index + 1 < siblings.Count)
        {
            if (!TryRemoveImageOnlySibling(siblings, index + 1))
            {
                break;
            }
        }
    }

    private static bool TryRemoveImageOnlySibling(List<HtmlNode> siblings, int index)
    {
        var node = siblings[index];
        if (node is HtmlText text)
        {
            if (string.IsNullOrWhiteSpace(Normalize(System.Net.WebUtility.HtmlDecode(text.Raw))))
            {
                // 纯空白文本：顺手删掉，继续看下一个相邻节点。
                siblings.RemoveAt(index);
                text.Parent = null;
                return true;
            }

            return false;
        }

        var element = (HtmlElement)node;
        var hasImage = element.Name == "img" || MiniHtml.Descendants(element).Any(d => d.Name == "img");
        var hasText = Normalize(MiniHtml.InnerText(element)).Length > 0;
        if (!hasImage || hasText)
        {
            return false;
        }

        MiniHtml.Remove(element);
        return true;
    }

    private static void RemoveLeadingTitle(HtmlElement root, string title)
    {
        var wanted = Normalize(System.Net.WebUtility.HtmlDecode(title));
        foreach (var element in MiniHtml.Descendants(root))
        {
            if (!LeafBlockNames.Contains(element.Name) || MiniHtml.Descendants(element).Any(d => BlockNames.Contains(d.Name)))
            {
                continue;
            }

            var text = Normalize(MiniHtml.InnerText(element));
            var hasImage = MiniHtml.Descendants(element).Any(d => d.Name == "img");
            if (text.Length == 0 && !hasImage)
            {
                continue; // 跳过开头的空占位
            }

            if (!hasImage && text == wanted)
            {
                MiniHtml.Remove(element);
            }

            return;
        }
    }

    // ---------------------------------------------------------------- 3. 清空白 / 化简

    /// <summary>自底向上删除没有文字、没有图片/分隔线的空节点（含 &lt;p&gt;&lt;br&gt;&lt;/p&gt; 之类空白占位段）。</summary>
    private static void Prune(HtmlElement element)
    {
        foreach (var child in element.Children.ToList())
        {
            if (child is HtmlElement childElement)
            {
                Prune(childElement);
            }
        }

        if (element.Name == "#root")
        {
            return;
        }

        if (element.Name is "img" or "hr" or "br" or "col")
        {
            return;
        }

        if (!HasVisibleContent(element))
        {
            MiniHtml.Remove(element);
        }
    }

    private static bool HasVisibleContent(HtmlElement element)
    {
        foreach (var child in element.Children)
        {
            switch (child)
            {
                case HtmlText text when Normalize(System.Net.WebUtility.HtmlDecode(text.Raw)).Length > 0:
                    return true;
                case HtmlElement { Name: "img" or "hr" }:
                    return true;
                case HtmlElement nested when HasVisibleContent(nested):
                    return true;
            }
        }

        return false;
    }

    /// <summary>脱掉无意义的壳：无属性 span；无属性、且只包着块级子节点的 div。并把首尾/连续的 br 收敛。</summary>
    private static void Simplify(HtmlElement element)
    {
        foreach (var child in element.Children.ToList())
        {
            if (child is HtmlElement childElement)
            {
                Simplify(childElement);
            }
        }

        if (element.Name == "#root")
        {
            return;
        }

        if (element.Attributes.Count == 0)
        {
            if (element.Name == "span")
            {
                MiniHtml.Unwrap(element);
                return;
            }

            if (element.Name == "div" && element.Parent is { } parent
                && (parent.Name is "div" or "#root" or "blockquote")
                && element.Children.All(c => c is HtmlElement e && BlockNames.Contains(e.Name) ||
                                             c is HtmlText t && string.IsNullOrWhiteSpace(t.Raw)))
            {
                MiniHtml.Unwrap(element);
                return;
            }
        }

        // 块内首尾的 <br> 没有意义；连续多个 <br> 收敛成一个。
        var children = element.Children;
        while (children.Count > 0 && IsBlankOrBreak(children[0]))
        {
            if (children[0] is HtmlElement { Name: "br" } || children[0] is HtmlText)
            {
                children[0].Parent = null;
                children.RemoveAt(0);
                continue;
            }

            break;
        }

        while (children.Count > 0 && IsBlankOrBreak(children[^1]))
        {
            if (children[^1] is HtmlElement { Name: "br" } || children[^1] is HtmlText)
            {
                children[^1].Parent = null;
                children.RemoveAt(children.Count - 1);
                continue;
            }

            break;
        }

        for (var i = children.Count - 1; i > 0; i--)
        {
            if (children[i] is HtmlElement { Name: "br" } && IsBreakRun(children, i))
            {
                children[i].Parent = null;
                children.RemoveAt(i);
            }
        }
    }

    private static bool IsBlankOrBreak(HtmlNode node) =>
        node is HtmlElement { Name: "br" } ||
        (node is HtmlText text && Normalize(System.Net.WebUtility.HtmlDecode(text.Raw)).Length == 0);

    private static bool IsBreakRun(List<HtmlNode> siblings, int index)
    {
        for (var j = index - 1; j >= 0; j--)
        {
            if (siblings[j] is HtmlElement { Name: "br" })
            {
                return true;
            }

            if (siblings[j] is HtmlText text && Normalize(System.Net.WebUtility.HtmlDecode(text.Raw)).Length == 0)
            {
                continue;
            }

            return false;
        }

        return false;
    }

    private static void TrimEdges(HtmlElement root)
    {
        while (root.Children.Count > 0 && root.Children[0] is HtmlText first &&
               string.IsNullOrWhiteSpace(System.Net.WebUtility.HtmlDecode(first.Raw)))
        {
            root.Children.RemoveAt(0);
        }

        while (root.Children.Count > 0 && root.Children[^1] is HtmlText last &&
               string.IsNullOrWhiteSpace(System.Net.WebUtility.HtmlDecode(last.Raw)))
        {
            root.Children.RemoveAt(root.Children.Count - 1);
        }
    }

    /// <summary>把零宽字符/不间断空格/连续空白归一，供文本规则匹配。</summary>
    internal static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '​' or '‌' or '‍' or '﻿' or '⁠':
                    break;
                case ' ' or '　' or ' ' or ' ' or ' ':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return Spaces.Replace(builder.ToString(), " ").Trim();
    }
}
