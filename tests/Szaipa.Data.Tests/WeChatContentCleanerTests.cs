using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>公众号正文清洗规则（不照搬公众号：白名单、剥内联样式、去关注引导/二维码/往期推荐/空段落…）。</summary>
public sealed class WeChatContentCleanerTests
{
    private static WeChatCleanedContent Clean(string html, string? title = null, Func<string, string?>? mapper = null, WeChatOptions? options = null) =>
        new WeChatContentCleaner(options).Clean(html, title, mapper);

    [Theory]
    [InlineData("https://user@mmbiz.qpic.cn/image/640")]
    [InlineData("https://user:pass@mmbiz.qpic.cn/image/640")]
    [InlineData("https://mmbiz.qpic.cn:8443/image/640")]
    public void Image_cleaning_rejects_userinfo_and_nondefault_ports(string url) =>
        Assert.Null(new WeChatContentCleaner().ResolveImageUrl(url));

    [Fact]
    public void Image_cleaning_upgrades_http_default_port_without_carrying_port_80_into_https() =>
        Assert.Equal("https://mmbiz.qpic.cn/image/640", new WeChatContentCleaner().ResolveImageUrl("http://mmbiz.qpic.cn:80/image/640"));

    [Fact]
    public void Strips_font_size_family_color_width_but_keeps_alignment_and_bold()
    {
        var result = Clean("<p style=\"font-size: 17px;font-family: 'Songti SC';color: rgb(1,2,3);text-align: center;line-height: 28px;width: 578px;\">" +
                           "正文<strong style=\"color:red;font-weight: bold;letter-spacing:2px\">加粗</strong></p>");

        Assert.Equal("<p style=\"text-align: center\">正文<strong style=\"font-weight: bold\">加粗</strong></p>", result.Html);
        Assert.DoesNotContain("font-size", result.Html);
        Assert.DoesNotContain("color", result.Html);
        Assert.DoesNotContain("width", result.Html);
    }

    [Fact]
    public void Drops_scripts_iframes_svg_event_handlers_and_unsafe_links()
    {
        var result = Clean(
            "<p onclick=\"evil()\">安全<script>alert(1)</script></p><iframe src=\"https://v.qq.com/x\"></iframe>" +
            "<svg><path d=\"M0\"/></svg><video src=\"a.mp4\"></video>" +
            "<p><a href=\"javascript:alert(1)\" onclick=\"x()\">坏链接</a> <a href=\"https://example.com/a?b=1&amp;c=2\">好链接</a></p>");

        Assert.DoesNotContain("script", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("svg", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("video", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("坏链接", result.Html); // 坏链接只脱壳，文字保留
        Assert.Contains("<a href=\"https://example.com/a?b=1&amp;c=2\" target=\"_blank\" rel=\"noopener noreferrer\">好链接</a>", result.Html);
    }

    [Fact]
    public void Unknown_tags_are_unwrapped_and_sections_become_divs_then_collapse()
    {
        var result = Clean("<section><section><font color=\"red\">文字</font></section><center>居中</center></section>");

        Assert.Equal("文字居中", Strip(result.Html));
        Assert.DoesNotContain("font", result.Html);
        Assert.DoesNotContain("section", result.Html);
        Assert.DoesNotContain("center", result.Html);
    }

    [Fact]
    public void Images_use_data_src_keep_only_src_alt_and_are_collected_in_order()
    {
        var result = Clean(
            "<p><img class=\"rich_pages\" data-ratio=\"0.5\" data-src=\"https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=jpeg&amp;wxfrom=5\" src=\"data:image/svg+xml,xx\" style=\"width: 578px !important\" alt=\"现场\" /></p>" +
            "<p><img data-src=\"//mmbiz.qpic.cn/mmbiz_png/BBB/640\" /></p>" +
            "<p><img data-src=\"https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=jpeg&amp;wxfrom=5\" /></p>");

        Assert.Equal(
            new[]
            {
                "https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=jpeg&wxfrom=5",
                "https://mmbiz.qpic.cn/mmbiz_png/BBB/640"
            },
            result.ImageUrls.ToArray());
        Assert.Contains("<img src=\"https://mmbiz.qpic.cn/mmbiz_jpg/AAA/640?wx_fmt=jpeg&amp;wxfrom=5\" alt=\"现场\" loading=\"lazy\" />", result.Html);
        Assert.DoesNotContain("data-src", result.Html);
        Assert.DoesNotContain("width", result.Html);
    }

    [Fact]
    public void Images_from_unlisted_hosts_are_dropped()
    {
        var result = Clean("<p>字</p><p><img src=\"https://evil.example.com/t.png\" /></p><p><img data-src=\"https://192.168.1.2/x.png\" /></p>");

        Assert.Empty(result.ImageUrls);
        Assert.DoesNotContain("<img", result.Html);
    }

    [Fact]
    public void Image_mapper_rewrites_to_local_urls_and_drops_failed_downloads()
    {
        var html = "<p><img data-src=\"https://mmbiz.qpic.cn/a/1/640\" /></p><p>中</p><p><img data-src=\"https://mmbiz.qpic.cn/a/2/640\" /></p>";

        var result = Clean(html, mapper: url => url.Contains("/1/") ? "/Content/WeChat/2026/10/h1.jpg" : null);

        Assert.Contains("src=\"/Content/WeChat/2026/10/h1.jpg\"", result.Html);
        Assert.DoesNotContain("qpic.cn", result.Html); // 绝不热链
        Assert.Equal(1, result.Html.Split("<img", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, result.ImageUrls.Count);
    }

    [Theory]
    [InlineData("点击上方蓝字关注我们")]
    [InlineData("点击蓝字 关注我们")]
    [InlineData("长按识别二维码关注")]
    [InlineData("长按二维码关注我们")]
    [InlineData("扫码关注我们")]
    [InlineData("欢迎关注本公众号")]
    [InlineData("阅读原文")]
    [InlineData("点击阅读原文")]
    [InlineData("别忘了点个赞和在看哦！")]
    [InlineData("点赞 在看 转发")]
    [InlineData("在看")]
    [InlineData("预览时标签不可点")]
    [InlineData("- END -")]
    [InlineData("· END ·")]
    [InlineData("往期推荐")]
    public void Noise_paragraphs_are_removed(string noise)
    {
        var result = Clean($"<p>这是一段正式的正文内容。</p><p style=\"text-align:center\"><span>{noise}</span></p><p>第二段正式内容。</p>");

        Assert.Equal("<p>这是一段正式的正文内容。</p><p>第二段正式内容。</p>", result.Html);
    }

    [Theory]
    [InlineData("收藏")]
    [InlineData("分享")]
    [InlineData("关注艺术市场的最新动向，是每一位收藏家的功课。")]
    [InlineData("我们将扫描仪放在了展厅的下方，用于识别藏品编号。")]
    public void Legitimate_short_content_is_not_mistaken_for_noise(string content)
    {
        var result = Clean($"<p>前文。</p><p>{content}</p><p>后文。</p>");

        Assert.Contains(content, result.Html);
    }

    [Fact]
    public void Qr_text_also_removes_the_adjacent_image_only_block()
    {
        var result = Clean(
            "<p>正文一</p><p><img data-src=\"https://mmbiz.qpic.cn/q/1/640\" /></p><p>扫描二维码关注我们</p><p><img data-src=\"https://mmbiz.qpic.cn/q/2/640\" /></p><p>正文二</p>");

        Assert.Equal("<p>正文一</p><p>正文二</p>", result.Html);
    }

    [Fact]
    public void Related_reading_tail_is_truncated_from_the_marker_onward()
    {
        var result = Clean(
            "<p>段落一</p><p>段落二</p><p>段落三</p><p>段落四</p><p>往期推荐</p><p><a href=\"https://mp.weixin.qq.com/s/a\">旧文一</a></p><p><a href=\"https://mp.weixin.qq.com/s/b\">旧文二</a></p><p><img data-src=\"https://mmbiz.qpic.cn/t/1/640\" /></p>");

        Assert.Equal("<p>段落一</p><p>段落二</p><p>段落三</p><p>段落四</p>", result.Html);
        Assert.DoesNotContain("旧文", result.Html);
    }

    [Fact]
    public void Tail_marker_near_the_beginning_is_not_trusted()
    {
        var result = Clean("<p>相关阅读</p><p>段落一</p><p>段落二</p><p>段落三</p><p>段落四</p><p>段落五</p>");

        Assert.Contains("段落五", result.Html); // 不会把整篇文章截断
    }

    [Fact]
    public void Empty_placeholder_paragraphs_hidden_elements_and_wechat_cards_are_removed()
    {
        var result = Clean(
            "<p><br /></p><p>&nbsp;</p><p>&#8203;</p><p><span><br/></span></p><p style=\"display: none;\"><mp-style-type data-value=\"3\"></mp-style-type></p>" +
            "<mp-common-profile data-nickname=\"x\"></mp-common-profile><p>真内容</p><p><br /></p><p>&nbsp;&nbsp;</p>");

        Assert.Equal("<p>真内容</p>", result.Html);
    }

    [Fact]
    public void Leading_block_that_repeats_the_title_is_removed()
    {
        var result = Clean("<p><br /></p><p style=\"font-size: 22px\">展览 &amp; 座谈</p><p>正文</p>", title: "展览 & 座谈");

        Assert.Equal("<p>正文</p>", result.Html);
    }

    [Fact]
    public void Extra_noise_patterns_from_options_apply()
    {
        var options = new WeChatOptions { Cleaning = { ExtraNoisePatterns = { "^本文由.+提供$" } } };

        var result = Clean("<p>正文</p><p>本文由某某机构提供</p>", options: options);

        Assert.Equal("<p>正文</p>", result.Html);
    }

    [Fact]
    public void Table_structure_and_headings_survive()
    {
        var result = Clean("<h2 style=\"font-size: 30px\">标题</h2><table style=\"width:100%\"><tbody><tr><td colspan=\"2\" style=\"width:50%\">甲</td></tr></tbody></table>");

        Assert.Equal("<h2>标题</h2><table><tbody><tr><td colspan=\"2\">甲</td></tr></tbody></table>", result.Html);
    }

    [Fact]
    public void FilterStyle_keeps_only_layout_neutral_declarations()
    {
        Assert.Equal("text-align: right; font-style: italic", WeChatContentCleaner.FilterStyle("text-align:RIGHT; font-style: italic; color: red; font-size: 40px !important; background-color: #fff"));
        Assert.Equal("", WeChatContentCleaner.FilterStyle("width: 100%; margin: 0"));
        Assert.Equal("text-decoration: underline", WeChatContentCleaner.FilterStyle("text-decoration: underline solid"));
    }

    private static string Strip(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
}
