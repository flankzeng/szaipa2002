using System.Text.Json;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>专题积木内容的规范化 / 白名单 / 清洗。这些规则是后台保存与前台渲染共同依赖的安全边界。</summary>
public sealed class PublicationBlockContentTests
{
    private static IReadOnlyDictionary<string, string> Norm(string type, string json) =>
        PublicationBlockContent.Parse(PublicationBlockContent.Normalize(type, json));

    [Fact]
    public void Normalize_rejects_unknown_block_types()
    {
        Assert.Null(PublicationBlockContent.Normalize("iframe", "{}"));
        Assert.Null(PublicationBlockContent.Normalize(null, "{}"));
        Assert.Null(PublicationBlockContent.Normalize("", "{}"));
    }

    [Fact]
    public void Normalize_every_known_type_returns_a_json_object()
    {
        foreach (var type in PublicationBlockTypes.All)
        {
            var json = PublicationBlockContent.Normalize(type, null);

            Assert.NotNull(json);
            using var document = JsonDocument.Parse(json!);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public void Normalize_keeps_only_the_keys_of_the_block_type_and_trims()
    {
        var content = Norm("quote", """{"text":"  好作品  ","author":"  某某 ","evil":"x","html":"<b>x</b>"}""");

        Assert.Equal("好作品", content["text"]);
        Assert.Equal("某某", content["author"]);
        Assert.False(content.ContainsKey("evil"));
        Assert.False(content.ContainsKey("html"));
    }

    [Fact]
    public void Normalize_treats_malformed_or_non_object_json_as_empty()
    {
        Assert.Equal(string.Empty, Norm("quote", "{not json")["text"]);
        Assert.Equal(string.Empty, Norm("quote", "[1,2]")["text"]);
        Assert.Equal(string.Empty, Norm("quote", """{"text":123}""")["text"]); // non-string values are ignored
    }

    [Theory]
    [InlineData("/Content/images/a/banner.jpg", true)]
    [InlineData("/media/derived/x.avif", true)]
    [InlineData("https://evil.example/x.jpg", false)]
    [InlineData("//evil.example/x.jpg", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/Content/../secret.jpg", false)]
    [InlineData("/Content/a\\b.jpg", false)]
    [InlineData("/Content/a\"onerror=\"x.jpg", false)]
    [InlineData("relative/path.jpg", false)]
    [InlineData("", false)]
    public void IsSafeLocalPath_only_accepts_root_relative_paths(string path, bool expected)
    {
        Assert.Equal(expected, PublicationBlockContent.IsSafeLocalPath(path));
    }

    [Fact]
    public void Banner_image_path_must_be_site_local()
    {
        Assert.Equal("/Content/b.jpg", Norm("banner", """{"imagePath":"/Content/b.jpg"}""")["imagePath"]);
        Assert.Equal(string.Empty, Norm("banner", """{"imagePath":"https://evil.example/b.jpg"}""")["imagePath"]);
    }

    [Fact]
    public void Richtext_html_is_sanitized_on_save()
    {
        var html = """<p>正文<script>alert(1)</script><a href="javascript:alert(1)" onclick="x()">链接</a></p><img src="/Content/a.jpg" onerror="x()">""";

        var saved = Norm("richtext", JsonSerializer.Serialize(new { heading = "标题", html }))["html"];

        Assert.DoesNotContain("script", saved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", saved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", saved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", saved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("正文", saved);
        Assert.Contains("<img src=\"/Content/a.jpg\"", saved);
    }

    // ---------- 视频 ----------

    [Fact]
    public void Video_local_requires_a_site_local_mp4_or_webm()
    {
        Assert.Equal("/Content/v/a.mp4", Norm("video", """{"source":"local","url":"/Content/v/a.mp4"}""")["url"]);
        Assert.Equal("/Content/v/a.webm", Norm("video", """{"source":"local","url":"/Content/v/a.webm"}""")["url"]);
        Assert.Equal(string.Empty, Norm("video", """{"source":"local","url":"/Content/v/a.exe"}""")["url"]);
        Assert.Equal(string.Empty, Norm("video", """{"source":"local","url":"https://x.example/a.mp4"}""")["url"]);
        Assert.Equal(string.Empty, Norm("video", """{"source":"local","url":"/Content/../a.mp4"}""")["url"]);
    }

    [Fact]
    public void Video_unknown_source_is_treated_as_local()
    {
        var content = Norm("video", """{"source":"weird","url":"/Content/v/a.mp4"}""");

        Assert.Equal("local", content["source"]);
        Assert.Equal("/Content/v/a.mp4", content["url"]);
    }

    [Theory]
    [InlineData("https://player.bilibili.com/player.html?bvid=BV1xx411c7mD", true)]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", true)]
    [InlineData("https://player.vimeo.com/video/76979871", true)]
    [InlineData("http://player.bilibili.com/player.html", false)]            // 必须 https
    [InlineData("https://evil.example/embed", false)]
    [InlineData("https://player.bilibili.com.evil.example/x", false)]         // 后缀伪装
    [InlineData("https://user:pw@player.bilibili.com/x", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void IsAllowedEmbedUrl_enforces_the_player_allow_list(string url, bool expected)
    {
        Assert.Equal(expected, PublicationBlockContent.IsAllowedEmbedUrl(url));
    }

    [Theory]
    [InlineData("https://www.bilibili.com/video/BV1xx411c7mD/?spm_id_from=333", "https://player.bilibili.com/player.html?bvid=BV1xx411c7mD&autoplay=0")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube.com/embed/dQw4w9WgXcQ")]
    public void Video_embed_converts_common_share_links(string shared, string expected)
    {
        var content = Norm("video", JsonSerializer.Serialize(new { source = "embed", url = shared }));

        Assert.Equal("embed", content["source"]);
        Assert.Equal(expected, content["url"]);
    }

    [Fact]
    public void Video_embed_with_a_foreign_host_is_emptied_and_drops_the_poster()
    {
        var content = Norm("video", """{"source":"embed","url":"https://evil.example/x","poster":"/Content/p.jpg"}""");

        Assert.Equal(string.Empty, content["url"]);
        Assert.Equal(string.Empty, content["poster"]);
    }

    // ---------- 清洗器 ----------

    [Fact]
    public void Sanitizer_keeps_tiptap_output()
    {
        const string html = "<h2>标题</h2><p style=\"text-align: center\"><strong>粗</strong><em>斜</em><u>线</u></p><ul><li>一</li></ul><blockquote><p>引</p></blockquote>";

        Assert.Equal(html, RichTextSanitizer.Sanitize(html));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>ok", "ok")]
    [InlineData("<style>p{}</style>ok", "ok")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>ok", "ok")]
    [InlineData("<div onclick=\"x()\">ok</div>", "ok")]
    [InlineData("<p onmouseover=\"x()\">ok</p>", "<p>ok</p>")]
    [InlineData("<!-- c --><p>ok</p>", "<p>ok</p>")]
    public void Sanitizer_drops_dangerous_markup(string input, string expected)
    {
        Assert.Equal(expected, RichTextSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitizer_only_keeps_safe_link_targets()
    {
        Assert.Contains("href=\"https://a.example/x\"", RichTextSanitizer.Sanitize("<a href=\"https://a.example/x\">x</a>"));
        Assert.Contains("rel=\"noopener noreferrer\"", RichTextSanitizer.Sanitize("<a href=\"https://a.example/x\">x</a>"));
        Assert.Equal("<a>x</a>", RichTextSanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>"));
        Assert.Equal("<a>x</a>", RichTextSanitizer.Sanitize("<a href=\"data:text/html;base64,AAAA\">x</a>"));
        Assert.Equal("<a>x</a>", RichTextSanitizer.Sanitize("<a href=\"//evil.example\">x</a>"));
        Assert.Contains("href=\"mailto:a@b.example\"", RichTextSanitizer.Sanitize("<a href=\"mailto:a@b.example\">x</a>"));
    }

    [Fact]
    public void Sanitizer_drops_images_without_a_safe_source_and_closes_unbalanced_tags()
    {
        Assert.Equal(string.Empty, RichTextSanitizer.Sanitize("<img src=\"javascript:alert(1)\">"));
        Assert.Equal(string.Empty, RichTextSanitizer.Sanitize("<img onerror=\"x()\">"));
        Assert.Equal("<p><strong>a</strong></p>", RichTextSanitizer.Sanitize("<p><strong>a"));
        Assert.Equal("<p>a</p>", RichTextSanitizer.Sanitize("<p>a</p></div>"));
    }

    [Fact]
    public void Sanitizer_escapes_stray_angle_brackets()
    {
        Assert.Equal("1 &lt; 2", RichTextSanitizer.Sanitize("1 < 2"));
    }
}
