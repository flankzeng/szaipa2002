using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class WeChatArticlePageParserTests
{
    private const string RealUrl = "https://mp.weixin.qq.com/s/2omPr817EGqhsHa8HR3txw";

    [Fact]
    public void Parses_a_real_article_page()
    {
        var article = WeChatArticlePageParser.Parse(WeChatTestSupport.Fixture("wechat-article-real.html"), RealUrl);

        Assert.Equal("不要错过！ | 艺术浪潮打造深圳名片 2021深圳湾公共艺术季启幕", article.Title);
        Assert.Equal("深圳人才公园", article.AccountName);
        Assert.Null(article.Author); // 公众号文章常无作者，导入时回退为公众号名
        Assert.Equal(new DateTime(2021, 11, 3, 18, 56, 21), article.PublishedAt); // ct=1635936981，北京时间
        Assert.Equal("mp:MzU5MDg4NzY4Mg==:2247493729:1", article.SourceKey);
        Assert.Equal(0, article.ItemIndex);
        Assert.Equal(RealUrl, article.SourceUrl);
        Assert.StartsWith("http://mmbiz.qpic.cn/mmbiz_jpg/eEWPwndpK7c1", article.CoverUrl);
        Assert.Contains("2021年11月1日—2022年1月10日", article.Digest);
        Assert.DoesNotContain("\\x0a", article.Digest); // JS 转义已还原
        Assert.Contains("data-src=\"https://mmbiz.qpic.cn", article.ContentHtml);
        Assert.Contains("人才公园", article.ContentHtml);
    }

    [Fact]
    public void Real_article_cleans_to_a_slim_body_without_inline_fonts()
    {
        var article = WeChatArticlePageParser.Parse(WeChatTestSupport.Fixture("wechat-article-real.html"), RealUrl);

        var cleaned = new WeChatContentCleaner().Clean(article.ContentHtml, article.Title);

        Assert.True(cleaned.ImageUrls.Count > 5);
        Assert.All(cleaned.ImageUrls, url => Assert.StartsWith("https://", url));
        Assert.DoesNotContain("font-size", cleaned.Html);
        Assert.DoesNotContain("font-family", cleaned.Html);
        Assert.DoesNotContain("<iframe", cleaned.Html);
        Assert.DoesNotContain("data-", cleaned.Html);
        Assert.True(cleaned.Html.Length < article.ContentHtml.Length / 2, "清洗后应显著瘦身");
        Assert.Contains("地铁", cleaned.PlainText);
    }

    [Fact]
    public void Noise_fixture_end_to_end_cleaning()
    {
        var article = WeChatArticlePageParser.Parse(WeChatTestSupport.Fixture("wechat-article-noise.html"), "https://mp.weixin.qq.com/s/NoiseSample01");

        Assert.Equal("同构第三季开幕 & 艺术家座谈", article.Title);
        Assert.Equal("深圳市艺术产业促进会", article.AccountName);
        Assert.Equal("mp:MzI5NDg4MDAyMw==:2247490001:1", article.SourceKey);

        var cleaned = new WeChatContentCleaner().Clean(article.ContentHtml, article.Title);

        // 保留：正文段落、2 张允许域名的内容图（封面不在正文里）
        Assert.Contains("同构第三季", cleaned.Html);
        Assert.Contains("展览由深圳市艺术产业促进会主办。", cleaned.Html);
        Assert.Contains("感谢各位到场。", cleaned.Html);
        Assert.Equal(
            new[] { "https://mmbiz.qpic.cn/mmbiz_jpg/AAAAAAAA/640?wx_fmt=jpeg&wxfrom=5", "https://mmbiz.qpic.cn/mmbiz_png/BBBBBBBB/640?wx_fmt=png" },
            cleaned.ImageUrls.Take(2).ToArray());
        // 去掉：关注引导 / 标题重复 / 二维码+配图 / 阅读原文 / 点赞在看 / 往期推荐及其后 / 隐藏 / 名片 / 危险内容
        foreach (var gone in new[] { "点击上方蓝字", "长按二维码", "阅读原文", "别忘了", "往期推荐", "上一期的展览回顾", "更早的活动预告", "隐藏文字", "mp-common-profile", "<script", "<iframe", "<svg", "javascript:", "evil.example.com", "192.168", "QRQRQRQR", "TAILTAIL", "onclick"})
        {
            Assert.DoesNotContain(gone, cleaned.Html);
        }

        Assert.DoesNotContain("<p>同构第三季开幕 &amp; 艺术家座谈</p>", cleaned.Html); // 标题重复段已删
        Assert.DoesNotContain("font-size", cleaned.Html);
        Assert.DoesNotContain("rgb(", cleaned.Html);
        Assert.Contains("正常链接", cleaned.Html);
    }

    [Fact]
    public void Missing_content_gives_a_readable_error()
    {
        var deleted = "<html><body><div class=\"weui-msg\">该内容已被发布者删除</div></body></html>";
        var captcha = "<html><body>环境异常 请完成验证后继续访问</body></html>";

        Assert.Contains("已被发布者删除", Assert.Throws<WeChatParseException>(() => WeChatArticlePageParser.Parse(deleted, RealUrl)).Message);
        Assert.Contains("人机验证", Assert.Throws<WeChatParseException>(() => WeChatArticlePageParser.Parse(captcha, RealUrl)).Message);
        Assert.Throws<WeChatParseException>(() => WeChatArticlePageParser.Parse("", RealUrl));
    }

    [Theory]
    [InlineData("\\x26amp;", "&amp;")]
    [InlineData("a\\x0ab", "a\nb")]
    [InlineData("\\u4e2d\\u6587", "中文")]
    [InlineData("it\\'s", "it's")]
    public void UnescapeJs_handles_common_escapes(string input, string expected) =>
        Assert.Equal(expected, WeChatArticlePageParser.UnescapeJs(input));
}

public sealed class WeChatSourceKeyTests
{
    [Fact]
    public void Short_and_long_links_normalize_and_long_links_yield_the_same_key_as_page_ids()
    {
        Assert.Equal("https://mp.weixin.qq.com/s/AbC-123_x", WeChatSourceKey.NormalizeArticleUrl("http://mp.weixin.qq.com/s/AbC-123_x#wechat_redirect"));

        var longUrl = "http://mp.weixin.qq.com/s?__biz=MzU5MDg4NzY4Mg%3D%3D&amp;mid=2247493729&amp;idx=1&amp;sn=abcdef&amp;chksm=zz&amp;scene=21#rd";
        // chksm 必须保留（缺了微信会跳人机验证），scene/fragment 去掉。
        Assert.Equal(
            "https://mp.weixin.qq.com/s?__biz=MzU5MDg4NzY4Mg%3D%3D&mid=2247493729&idx=1&sn=abcdef&chksm=zz",
            WeChatSourceKey.NormalizeArticleUrl(longUrl));
        Assert.Equal(
            "https://mp.weixin.qq.com/s?__biz=MzU5MDg4NzY4Mg%3D%3D&mid=2247493729&idx=1&sn=abcdef",
            WeChatSourceKey.NormalizeArticleUrl("https://mp.weixin.qq.com/s?__biz=MzU5MDg4NzY4Mg==&mid=2247493729&idx=1&sn=abcdef&token=1006457716&lang=zh_CN#rd"));
        Assert.Equal("mp:MzU5MDg4NzY4Mg==:2247493729:1", WeChatSourceKey.TryFromLongUrl(longUrl));
        Assert.Equal(WeChatSourceKey.FromIds("MzU5MDg4NzY4Mg==", "2247493729", "1"), WeChatSourceKey.TryFromLongUrl(longUrl));
    }

    [Theory]
    [InlineData("https://evil.example.com/s/abc")]
    [InlineData("https://mp.weixin.qq.com.evil.com/s/abc")]
    [InlineData("https://mp.weixin.qq.com:8443/s/abc")]
    [InlineData("https://user@mp.weixin.qq.com/s/abc")]
    [InlineData("https://user:password@mp.weixin.qq.com/s/abc")]
    [InlineData("http://mp.weixin.qq.com:8080/s/abc")]
    [InlineData("https://mp.weixin.qq.com/other/abc")]
    [InlineData("ftp://mp.weixin.qq.com/s/abc")]
    [InlineData("https://mp.weixin.qq.com/s?__biz=x&mid=1")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Non_article_links_are_rejected(string input) =>
        Assert.Null(WeChatSourceKey.NormalizeArticleUrl(input));

    [Fact]
    public void Explicit_scheme_default_port_is_normalized_away()
    {
        Assert.Equal("https://mp.weixin.qq.com/s/abc", WeChatSourceKey.NormalizeArticleUrl("https://mp.weixin.qq.com:443/s/abc"));
    }
}
