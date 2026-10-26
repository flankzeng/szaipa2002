using System.Text.Json;
using System.Text.RegularExpressions;
using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services;

/// <summary>
/// Normalisation + typed access for <see cref="PublicationBlock.Content"/>.
///
/// Every block's payload is a flat JSON object of string values. <see cref="Normalize"/> runs on every admin
/// save (and is re-applied defensively by the public renderer through <see cref="Parse"/>): it keeps only the
/// keys the block type understands, trims text, sanitizes the rich-text HTML, restricts image/video paths to
/// site-local roots and restricts embedded video to an allow-list of players. Nothing the renderer prints
/// unescaped (<c>html</c>, <c>embedUrl</c>) can therefore carry script or an arbitrary iframe source.
/// </summary>
public static class PublicationBlockContent
{
    public const int MaxTextLength = 20_000;
    public const int MaxHtmlLength = 200_000;

    private static readonly IReadOnlyDictionary<string, string[]> AllowedKeys =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [PublicationBlockTypes.Banner] = new[] { "imagePath", "title", "subtitle" },
            [PublicationBlockTypes.Preface] = new[] { "heading", "body", "signature" },
            [PublicationBlockTypes.RichText] = new[] { "heading", "html" },
            [PublicationBlockTypes.Works] = new[] { "heading" },
            [PublicationBlockTypes.Gallery] = new[] { "heading" },
            [PublicationBlockTypes.Video] = new[] { "source", "url", "poster", "caption" },
            [PublicationBlockTypes.Quote] = new[] { "text", "author" }
        };

    private static readonly HashSet<string> PathKeys = new(StringComparer.Ordinal) { "imagePath", "poster" };

    /// <summary>Embed hosts whose player pages may be iframed (https only). Exact host or its sub-domain.</summary>
    private static readonly string[] EmbedHosts =
    {
        "player.bilibili.com", "www.youtube.com", "www.youtube-nocookie.com", "player.vimeo.com",
        "v.qq.com", "player.youku.com"
    };

    private static readonly Regex BilibiliBvid = new(@"bilibili\.com/video/(?<id>BV[0-9A-Za-z]+)", RegexOptions.Compiled);
    private static readonly Regex YouTubeWatch = new(@"^https?://(?:www\.)?youtube\.com/watch\?(?:.*&)?v=(?<id>[\w-]{6,20})", RegexOptions.Compiled);
    private static readonly Regex YouTubeShort = new(@"^https?://youtu\.be/(?<id>[\w-]{6,20})", RegexOptions.Compiled);

    /// <summary>
    /// Returns the normalised JSON for <paramref name="blockType"/>, or <c>null</c> when the type is unknown.
    /// Malformed / non-object JSON becomes an empty payload for a known type.
    /// </summary>
    public static string? Normalize(string? blockType, string? json)
    {
        if (!PublicationBlockTypes.IsKnown(blockType))
        {
            return null;
        }

        var input = ReadStringProperties(json);
        var output = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in AllowedKeys[blockType!])
        {
            input.TryGetValue(key, out var raw);
            var value = (raw ?? string.Empty).Trim();

            if (key == "html")
            {
                value = RichTextSanitizer.Sanitize(Truncate(value, MaxHtmlLength));
            }
            else
            {
                value = Truncate(value, MaxTextLength);
            }

            if (PathKeys.Contains(key))
            {
                value = IsSafeLocalPath(value) ? value : string.Empty;
            }

            output[key] = value;
        }

        if (blockType == PublicationBlockTypes.Video)
        {
            NormalizeVideo(output);
        }

        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>Reads a block payload as a flat string map; malformed JSON yields an empty map. Never throws.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string? json) => ReadStringProperties(json);

    /// <summary>Value of <paramref name="key"/> or an empty string.</summary>
    public static string Get(IReadOnlyDictionary<string, string> content, string key) =>
        content.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>
    /// A path the site may serve: root-relative, no scheme/host, no traversal, no control characters
    /// (<c>/Content/...</c> legacy assets, <c>/media/...</c> derived files). Empty is not "safe" – callers decide.
    /// </summary>
    public static bool IsSafeLocalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var path = value.Trim();
        return path.StartsWith('/')
            && !path.StartsWith("//", StringComparison.Ordinal)
            && !path.Contains("..", StringComparison.Ordinal)
            && !path.Contains('\\')
            && !path.Any(char.IsControl)
            && !path.Contains('<') && !path.Contains('>') && !path.Contains('"') && !path.Contains('\'');
    }

    /// <summary>Whether <paramref name="url"/> is an https player page on the embed allow-list.</summary>
    public static bool IsAllowedEmbedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return EmbedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    private static void NormalizeVideo(Dictionary<string, string> output)
    {
        var source = output["source"].ToLowerInvariant();
        var url = output["url"];

        if (source == "embed")
        {
            url = ToEmbedUrl(url);
            output["url"] = IsAllowedEmbedUrl(url) ? url : string.Empty;
            output["poster"] = string.Empty;
        }
        else
        {
            source = "local";
            var extension = Path.GetExtension(url).ToLowerInvariant();
            output["url"] = IsSafeLocalPath(url) && extension is ".mp4" or ".webm" ? url : string.Empty;
        }

        output["source"] = source;
    }

    /// <summary>Converts common share links (B 站 / YouTube watch) to their embeddable player URL.</summary>
    internal static string ToEmbedUrl(string url)
    {
        var bilibili = BilibiliBvid.Match(url);
        if (bilibili.Success)
        {
            return $"https://player.bilibili.com/player.html?bvid={bilibili.Groups["id"].Value}&autoplay=0";
        }

        var youtube = YouTubeWatch.Match(url);
        if (youtube.Success)
        {
            return "https://www.youtube.com/embed/" + youtube.Groups["id"].Value;
        }

        var shortLink = YouTubeShort.Match(url);
        return shortLink.Success ? "https://www.youtube.com/embed/" + shortLink.Groups["id"].Value : url;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static Dictionary<string, string> ReadStringProperties(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed payload: treat as empty so a bad row can never take the page down.
        }

        return result;
    }
}
