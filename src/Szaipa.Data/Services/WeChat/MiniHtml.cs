using System.Net;
using System.Text;

namespace Szaipa.Data.Services.WeChat;

// 一个只为"清洗公众号正文"服务的极简、容错 HTML 解析器 + 序列化器（不引入第三方库）。
// 公众号文章 HTML 基本格式良好，但夹杂 <img ... />、自定义标签（mp-*）、内联 svg 等；这里只需要：
//   * 把标签流还原成树，方便做"删节点 / 脱壳 / 按文本判断噪声"；
//   * 序列化时对属性值重新转义，文本保持原样（原文本里的实体不解码、不重编码，避免破坏内容）。
// 脚本类标签（script/style 等）的内容按原始文本跳过，不会被当成标记。

internal abstract class HtmlNode
{
    public HtmlElement? Parent { get; set; }
}

internal sealed class HtmlText : HtmlNode
{
    public HtmlText(string raw) => Raw = raw;

    /// <summary>源码中的原始文本（实体未解码）。</summary>
    public string Raw { get; set; }
}

internal sealed class HtmlElement : HtmlNode
{
    public HtmlElement(string name) => Name = name;

    /// <summary>小写标签名；根节点为 <c>#root</c>。</summary>
    public string Name { get; set; }

    /// <summary>属性（名小写，值已做 HTML 解码），保持源码顺序。</summary>
    public List<KeyValuePair<string, string>> Attributes { get; } = new();

    public List<HtmlNode> Children { get; } = new();

    public string? GetAttribute(string name)
    {
        foreach (var attribute in Attributes)
        {
            if (attribute.Key == name)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    public void SetAttribute(string name, string value)
    {
        for (var i = 0; i < Attributes.Count; i++)
        {
            if (Attributes[i].Key == name)
            {
                Attributes[i] = new KeyValuePair<string, string>(name, value);
                return;
            }
        }

        Attributes.Add(new KeyValuePair<string, string>(name, value));
    }

    public void Append(HtmlNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public IEnumerable<HtmlElement> ChildElements => Children.OfType<HtmlElement>();
}

internal static class MiniHtml
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.Ordinal)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.Ordinal)
    {
        "script", "style", "textarea", "title", "noscript", "template"
    };

    public static bool IsVoid(string name) => VoidElements.Contains(name);

    public static HtmlElement Parse(string html)
    {
        var root = new HtmlElement("#root");
        var stack = new List<HtmlElement> { root };
        var i = 0;
        var length = html.Length;
        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length > 0)
            {
                stack[^1].Append(new HtmlText(text.ToString()));
                text.Clear();
            }
        }

        while (i < length)
        {
            var c = html[i];
            if (c != '<')
            {
                var next = html.IndexOf('<', i);
                if (next < 0)
                {
                    next = length;
                }

                text.Append(html, i, next - i);
                i = next;
                continue;
            }

            // 注释 / doctype / 处理指令：整段丢弃。
            if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? length : end + 3;
                continue;
            }

            if (i + 1 < length && (html[i + 1] == '!' || html[i + 1] == '?'))
            {
                var end = html.IndexOf('>', i);
                i = end < 0 ? length : end + 1;
                continue;
            }

            // 结束标签
            if (i + 2 < length && html[i + 1] == '/' && IsNameStart(html[i + 2]))
            {
                var end = html.IndexOf('>', i);
                if (end < 0)
                {
                    end = length - 1;
                }

                var name = ReadName(html, i + 2, end).ToLowerInvariant();
                FlushText();
                for (var s = stack.Count - 1; s >= 1; s--)
                {
                    if (stack[s].Name == name)
                    {
                        stack.RemoveRange(s, stack.Count - s);
                        break;
                    }
                }

                i = end + 1;
                continue;
            }

            // 开始标签
            if (i + 1 < length && IsNameStart(html[i + 1]))
            {
                var element = ReadStartTag(html, ref i, out var selfClosing);
                FlushText();
                stack[^1].Append(element);

                if (RawTextElements.Contains(element.Name))
                {
                    // 原始文本元素：跳到对应结束标签，内容整体作为一个文本节点（后续会被整体丢弃）。
                    var close = html.IndexOf("</" + element.Name, i, StringComparison.OrdinalIgnoreCase);
                    var contentEnd = close < 0 ? length : close;
                    if (contentEnd > i)
                    {
                        element.Append(new HtmlText(html.Substring(i, contentEnd - i)));
                    }

                    if (close < 0)
                    {
                        i = length;
                    }
                    else
                    {
                        var gt = html.IndexOf('>', close);
                        i = gt < 0 ? length : gt + 1;
                    }

                    continue;
                }

                if (!selfClosing && !VoidElements.Contains(element.Name))
                {
                    stack.Add(element);
                }

                continue;
            }

            // 孤立的 '<'：当作普通文本（序列化时会转义为 &lt;）。
            text.Append("&lt;");
            i++;
        }

        FlushText();
        return root;
    }

    private static bool IsNameStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static string ReadName(string html, int start, int limit)
    {
        var end = start;
        while (end < limit && !char.IsWhiteSpace(html[end]) && html[end] != '/' && html[end] != '>')
        {
            end++;
        }

        return html.Substring(start, end - start);
    }

    private static HtmlElement ReadStartTag(string html, ref int i, out bool selfClosing)
    {
        // i 指向 '<'
        var pos = i + 1;
        var length = html.Length;
        var nameEnd = pos;
        while (nameEnd < length && !char.IsWhiteSpace(html[nameEnd]) && html[nameEnd] != '/' && html[nameEnd] != '>')
        {
            nameEnd++;
        }

        var element = new HtmlElement(html.Substring(pos, nameEnd - pos).ToLowerInvariant());
        pos = nameEnd;
        selfClosing = false;

        while (pos < length)
        {
            while (pos < length && char.IsWhiteSpace(html[pos]))
            {
                pos++;
            }

            if (pos >= length)
            {
                break;
            }

            if (html[pos] == '>')
            {
                pos++;
                break;
            }

            if (html[pos] == '/')
            {
                if (pos + 1 < length && html[pos + 1] == '>')
                {
                    selfClosing = true;
                    pos += 2;
                    break;
                }

                pos++;
                continue;
            }

            var attributeStart = pos;
            while (pos < length && !char.IsWhiteSpace(html[pos]) && html[pos] != '=' && html[pos] != '>'
                   && !(html[pos] == '/' && pos + 1 < length && html[pos + 1] == '>'))
            {
                pos++;
            }

            var attributeName = html.Substring(attributeStart, pos - attributeStart).ToLowerInvariant();
            while (pos < length && char.IsWhiteSpace(html[pos]))
            {
                pos++;
            }

            var value = string.Empty;
            if (pos < length && html[pos] == '=')
            {
                pos++;
                while (pos < length && char.IsWhiteSpace(html[pos]))
                {
                    pos++;
                }

                if (pos < length && (html[pos] == '"' || html[pos] == '\''))
                {
                    var quote = html[pos];
                    var close = html.IndexOf(quote, pos + 1);
                    if (close < 0)
                    {
                        close = length;
                    }

                    value = html.Substring(pos + 1, close - pos - 1);
                    pos = Math.Min(length, close + 1);
                }
                else
                {
                    var valueStart = pos;
                    while (pos < length && !char.IsWhiteSpace(html[pos]) && html[pos] != '>')
                    {
                        pos++;
                    }

                    value = html.Substring(valueStart, pos - valueStart);
                }
            }

            if (attributeName.Length > 0 && element.GetAttribute(attributeName) is null)
            {
                element.Attributes.Add(new KeyValuePair<string, string>(attributeName, WebUtility.HtmlDecode(value)));
            }
        }

        i = pos;
        return element;
    }

    /// <summary>序列化子节点（不含根自身的标签）。</summary>
    public static string SerializeChildren(HtmlElement element)
    {
        var builder = new StringBuilder();
        foreach (var child in element.Children)
        {
            Serialize(child, builder);
        }

        return builder.ToString();
    }

    private static void Serialize(HtmlNode node, StringBuilder builder)
    {
        if (node is HtmlText text)
        {
            builder.Append(text.Raw);
            return;
        }

        var element = (HtmlElement)node;
        builder.Append('<').Append(element.Name);
        foreach (var attribute in element.Attributes)
        {
            builder.Append(' ').Append(attribute.Key).Append("=\"").Append(EncodeAttribute(attribute.Value)).Append('"');
        }

        if (VoidElements.Contains(element.Name))
        {
            builder.Append(" />");
            return;
        }

        builder.Append('>');
        foreach (var child in element.Children)
        {
            Serialize(child, builder);
        }

        builder.Append("</").Append(element.Name).Append('>');
    }

    private static string EncodeAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>节点内全部文本（HTML 解码后）。</summary>
    public static string InnerText(HtmlNode node)
    {
        var builder = new StringBuilder();
        AppendText(node, builder);
        return builder.ToString();
    }

    private static void AppendText(HtmlNode node, StringBuilder builder)
    {
        switch (node)
        {
            case HtmlText text:
                builder.Append(WebUtility.HtmlDecode(text.Raw));
                break;
            case HtmlElement element:
                if (element.Name is "script" or "style")
                {
                    return;
                }

                foreach (var child in element.Children)
                {
                    AppendText(child, builder);
                }

                break;
        }
    }

    public static void Remove(HtmlNode node)
    {
        node.Parent?.Children.Remove(node);
        node.Parent = null;
    }

    /// <summary>用 <paramref name="element"/> 的子节点替换它自身（脱壳）。</summary>
    public static void Unwrap(HtmlElement element)
    {
        var parent = element.Parent;
        if (parent is null)
        {
            return;
        }

        var index = parent.Children.IndexOf(element);
        parent.Children.RemoveAt(index);
        var children = element.Children.ToList();
        foreach (var child in children)
        {
            child.Parent = parent;
        }

        parent.Children.InsertRange(index, children);
        element.Parent = null;
        element.Children.Clear();
    }

    public static IEnumerable<HtmlElement> Descendants(HtmlElement root)
    {
        foreach (var child in root.Children)
        {
            if (child is HtmlElement element)
            {
                yield return element;
                foreach (var nested in Descendants(element))
                {
                    yield return nested;
                }
            }
        }
    }
}
