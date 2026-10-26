using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Szaipa.Data.Services;

/// <summary>
/// Small allow-list HTML sanitizer for staff-authored TipTap output (专题页「富文本」块). It keeps the
/// handful of tags TipTap's StarterKit/Link/Image/TextAlign emit, drops everything else (script/style/iframe
/// content included), strips all attributes except a short per-tag allow-list and rejects <c>javascript:</c> /
/// <c>data:</c> style URLs. It is deliberately conservative rather than a general-purpose sanitizer; the
/// editor is staff-only, this is defence in depth for stored content.
/// </summary>
public static class RichTextSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "hr", "strong", "b", "em", "i", "u", "s", "strike", "del", "blockquote",
        "ul", "ol", "li", "h2", "h3", "h4", "a", "img"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase) { "br", "hr", "img" };

    private static readonly HashSet<string> DropWithContentTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "noscript", "template", "textarea", "title", "head", "svg", "math"
    };

    private static readonly Regex CommentPattern = new(@"<!--.*?(-->|$)", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagPattern = new(
        @"<(?<close>/?)(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>(?:\s+[^\s""'<>/=]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'>]+))?)*)\s*(?<self>/?)>",
        RegexOptions.Compiled);

    private static readonly Regex AttributePattern = new(
        @"(?<name>[^\s""'<>/=]+)(?:\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s""'>]+)))?",
        RegexOptions.Compiled);

    private static readonly Regex TextAlignPattern = new(
        @"^\s*text-align\s*:\s*(left|center|right|justify)\s*;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DimensionPattern = new(@"^\d{1,4}$", RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        html = CommentPattern.Replace(html, string.Empty);

        var output = new StringBuilder(html.Length);
        var openTags = new Stack<string>();
        var position = 0;

        while (position < html.Length)
        {
            var next = html.IndexOf('<', position);
            if (next < 0)
            {
                AppendText(output, html, position, html.Length - position);
                break;
            }

            AppendText(output, html, position, next - position);

            var match = TagPattern.Match(html, next);
            if (!match.Success || match.Index != next)
            {
                // A stray '<' that is not a well-formed tag: keep it as text.
                output.Append("&lt;");
                position = next + 1;
                continue;
            }

            position = next + match.Length;
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var isClose = match.Groups["close"].Length > 0;

            if (!isClose && DropWithContentTags.Contains(name))
            {
                position = SkipElement(html, position, name);
                continue;
            }

            if (!AllowedTags.Contains(name))
            {
                continue; // unknown tag: drop the tag, keep its (escaped) text children
            }

            if (isClose)
            {
                if (!VoidTags.Contains(name) && openTags.Contains(name))
                {
                    // Close any still-open inner tags first so the output stays well-nested.
                    while (openTags.Count > 0)
                    {
                        var top = openTags.Pop();
                        output.Append("</").Append(top).Append('>');
                        if (top == name)
                        {
                            break;
                        }
                    }
                }

                continue;
            }

            var attributes = BuildAttributes(name, match.Groups["attrs"].Value);
            if (name == "img" && !attributes.Contains("src=", StringComparison.Ordinal))
            {
                continue; // an image without a safe source is useless
            }

            output.Append('<').Append(name).Append(attributes);
            if (VoidTags.Contains(name))
            {
                output.Append(" />");
            }
            else
            {
                output.Append('>');
                openTags.Push(name);
            }
        }

        while (openTags.Count > 0)
        {
            output.Append("</").Append(openTags.Pop()).Append('>');
        }

        return output.ToString().Trim();
    }

    private static void AppendText(StringBuilder output, string html, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        // TipTap already emits entity-encoded text; escape any raw '>' / '"' that slipped in unencoded.
        output.Append(html.AsSpan(start, length).ToString().Replace(">", "&gt;", StringComparison.Ordinal));
    }

    private static int SkipElement(string html, int position, string name)
    {
        var closeIndex = html.IndexOf("</" + name, position, StringComparison.OrdinalIgnoreCase);
        if (closeIndex < 0)
        {
            return html.Length; // unterminated: drop the rest
        }

        var end = html.IndexOf('>', closeIndex);
        return end < 0 ? html.Length : end + 1;
    }

    private static string BuildAttributes(string tag, string rawAttributes)
    {
        string? href = null, src = null, alt = null, width = null, height = null, style = null;

        foreach (Match attribute in AttributePattern.Matches(rawAttributes))
        {
            var attributeName = attribute.Groups["name"].Value.ToLowerInvariant();
            var value = WebUtility.HtmlDecode(
                attribute.Groups["dq"].Success ? attribute.Groups["dq"].Value
                : attribute.Groups["sq"].Success ? attribute.Groups["sq"].Value
                : attribute.Groups["bare"].Value);

            switch (attributeName)
            {
                case "href" when tag == "a":
                    href = IsSafeUrl(value, allowMailTel: true) ? value : null;
                    break;
                case "src" when tag == "img":
                    src = IsSafeUrl(value, allowMailTel: false) ? value : null;
                    break;
                case "alt" when tag == "img":
                    alt = value;
                    break;
                case "width" when tag == "img" && DimensionPattern.IsMatch(value):
                    width = value;
                    break;
                case "height" when tag == "img" && DimensionPattern.IsMatch(value):
                    height = value;
                    break;
                case "style" when tag is "p" or "h2" or "h3" or "h4":
                    var alignment = TextAlignPattern.Match(value);
                    style = alignment.Success ? "text-align: " + alignment.Groups[1].Value.ToLowerInvariant() : null;
                    break;
            }
        }

        var builder = new StringBuilder();
        if (href is not null)
        {
            builder.Append(" href=\"").Append(WebUtility.HtmlEncode(href)).Append('"');
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
            }
        }

        if (src is not null)
        {
            builder.Append(" src=\"").Append(WebUtility.HtmlEncode(src)).Append('"');
            builder.Append(" alt=\"").Append(WebUtility.HtmlEncode(alt ?? string.Empty)).Append('"');
            builder.Append(" loading=\"lazy\" decoding=\"async\"");
            if (width is not null)
            {
                builder.Append(" width=\"").Append(width).Append('"');
            }

            if (height is not null)
            {
                builder.Append(" height=\"").Append(height).Append('"');
            }
        }

        if (style is not null)
        {
            builder.Append(" style=\"").Append(style).Append('"');
        }

        return builder.ToString();
    }

    /// <summary>http(s), root-relative (<c>/…</c>), in-page (<c>#…</c>) and optionally mailto:/tel: only.</summary>
    internal static bool IsSafeUrl(string value, bool allowMailTel)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Any(char.IsControl))
        {
            return false;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return false; // protocol-relative: ambiguous, refuse
        }

        if (value.StartsWith('/') || value.StartsWith('#'))
        {
            return !value.Contains("..", StringComparison.Ordinal);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.Scheme is "http" or "https"
                || (allowMailTel && uri.Scheme is "mailto" or "tel");
        }

        return false;
    }
}
