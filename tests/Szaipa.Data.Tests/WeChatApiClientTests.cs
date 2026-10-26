using System.Net;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>官方接口客户端：全部走假 HttpMessageHandler，不访问外网。</summary>
public sealed class WeChatApiClientTests
{
    private const string Secret = "TOPSECRET-VALUE";
    private const string Token = "ACCESS-TOKEN-VALUE";

    private static readonly WeChatOptions Options = new() { Enabled = true, AppId = "wx1234567890abcd", AppSecret = Secret };

    private const string TokenJson = "{\"access_token\":\"" + Token + "\",\"expires_in\":7200}";

    private const string PageJson = """
        {"total_count":3,"item_count":2,"item":[
          {"article_id":"A1","update_time":1761471600,"content":{"create_time":1761400000,"news_item":[
            {"title":"头条","author":"甲","digest":"摘要一","content":"<p>正文一</p>","url":"http://mp.weixin.qq.com/s?__biz=MzI5&mid=11&idx=1&sn=aa&chksm=x#rd","thumb_url":"http://mmbiz.qpic.cn/c1/0","is_deleted":false},
            {"title":"二条","author":"","digest":"摘要二","content":"<p>正文二</p>","url":"http://mp.weixin.qq.com/s?__biz=MzI5&mid=11&idx=2&sn=bb&chksm=x#rd","thumb_url":"http://mmbiz.qpic.cn/c2/0"},
            {"title":"已删","content":"<p>x</p>","url":"http://mp.weixin.qq.com/s?__biz=MzI5&mid=11&idx=3&sn=cc","is_deleted":true}
          ]}},
          {"media_id":"M2","content":{"create_time":1761000000,"news_item":[
            {"title":"没有链接的草稿","content":"<p>d</p>","url":""}
          ]},"update_time":1761000001}
        ]}
        """;

    private static (WeChatApiClient Client, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> route)
    {
        var handler = new FakeHandler(route);
        return (new WeChatApiClient(new FakeHttpClientFactory(handler), WeChatTestSupport.Opts(Options)), handler);
    }

    [Fact]
    public async Task Freepublish_page_is_parsed_and_the_token_is_cached_across_calls()
    {
        var (client, handler) = Create(request => request.RequestUri!.AbsolutePath switch
        {
            "/cgi-bin/stable_token" => FakeHandler.Json(TokenJson),
            "/cgi-bin/freepublish/batchget" => FakeHandler.Json(PageJson),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var first = await client.GetPublishedPageAsync(0, 20, CancellationToken.None);
        await client.GetPublishedPageAsync(20, 20, CancellationToken.None);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.ItemCount);
        Assert.Equal(new[] { "头条", "二条" }, first.Articles.Select(a => a.Title).ToArray()); // 已删/无链接的被跳过
        Assert.Equal("A1", first.Articles[0].ArticleId);
        Assert.Equal(1, first.Articles[1].ItemIndex);
        Assert.Equal(new DateTime(2025, 10, 26, 17, 40, 0), first.Articles[0].PublishedAt); // 1761471600 → 北京时间
        Assert.Equal(1, handler.Requests.Count(r => r.Contains("stable_token"))); // 日额度有限：token 只取一次
        Assert.Equal(2, handler.Requests.Count(r => r.Contains("freepublish/batchget")));
    }

    [Fact]
    public async Task Material_news_page_posts_type_news_and_reuses_the_same_shape()
    {
        string? body = null;
        var (client, _) = Create(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/cgi-bin/stable_token")
            {
                return FakeHandler.Json(TokenJson);
            }

            body = request.Content!.ReadAsStringAsync().Result;
            return FakeHandler.Json(PageJson);
        });

        var page = await client.GetMaterialNewsPageAsync(40, 99, CancellationToken.None);

        Assert.Contains("\"type\":\"news\"", body);
        Assert.Contains("\"offset\":40", body);
        Assert.Contains("\"count\":20", body); // count 被限制到 20
        Assert.Equal(2, page.Articles.Count);
    }

    [Fact]
    public async Task Material_count_is_a_get_and_parsed()
    {
        HttpMethod? method = null;
        var (client, _) = Create(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/cgi-bin/stable_token")
            {
                return FakeHandler.Json(TokenJson);
            }

            method = request.Method;
            return FakeHandler.Json("{\"voice_count\":1,\"video_count\":2,\"image_count\":3,\"news_count\":4}");
        });

        var count = await client.GetMaterialCountAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal(new WeChatMaterialCount(1, 2, 3, 4), count);
    }

    [Theory]
    [InlineData(48001, "未开通")]
    [InlineData(50001, "未开通")]
    [InlineData(40164, "IP 白名单")]
    [InlineData(45009, "上限")]
    public async Task Api_error_codes_become_readable_messages_without_secrets(int errorCode, string expectedFragment)
    {
        var (client, _) = Create(request => request.RequestUri!.AbsolutePath == "/cgi-bin/stable_token"
            ? FakeHandler.Json(TokenJson)
            : FakeHandler.Json($"{{\"errcode\":{errorCode},\"errmsg\":\"api unauthorized rid: abc\"}}"));

        var exception = await Assert.ThrowsAsync<WeChatApiException>(() => client.GetPublishedPageAsync(0, 20, CancellationToken.None));

        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Contains(expectedFragment, exception.Message);
        Assert.DoesNotContain(Secret, exception.Message);
        Assert.DoesNotContain(Token, exception.Message);
    }

    [Fact]
    public async Task Token_endpoint_errors_are_reported_for_credentials_and_ip_whitelist()
    {
        var (client, _) = Create(_ => FakeHandler.Json("{\"errcode\":40164,\"errmsg\":\"invalid ip 1.2.3.4, not in whitelist\"}"));

        var exception = await Assert.ThrowsAsync<WeChatApiException>(() => client.VerifyCredentialsAsync(CancellationToken.None));

        Assert.Equal(40164, exception.ErrorCode);
        Assert.Contains("IP", exception.Message);
    }

    [Fact]
    public async Task Expired_token_error_forces_one_refresh_and_retries()
    {
        var tokenCalls = 0;
        var batchCalls = 0;
        string? lastTokenBody = null;
        var (client, _) = Create(request =>
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/cgi-bin/stable_token":
                    tokenCalls++;
                    lastTokenBody = request.Content!.ReadAsStringAsync().Result;
                    return FakeHandler.Json(TokenJson);
                default:
                    return ++batchCalls == 1
                        ? FakeHandler.Json("{\"errcode\":42001,\"errmsg\":\"access_token expired\"}")
                        : FakeHandler.Json(PageJson);
            }
        });

        var page = await client.GetPublishedPageAsync(0, 20, CancellationToken.None);

        Assert.Equal(2, page.Articles.Count);
        Assert.Equal(2, tokenCalls);
        Assert.Equal(2, batchCalls);
        Assert.Contains("\"force_refresh\":true", lastTokenBody);
    }

    [Fact]
    public async Task Http_failures_do_not_leak_the_request_url_or_token()
    {
        var (client, _) = Create(request => request.RequestUri!.AbsolutePath == "/cgi-bin/stable_token"
            ? FakeHandler.Json(TokenJson)
            : new HttpResponseMessage(HttpStatusCode.BadGateway));

        var exception = await Assert.ThrowsAsync<WeChatApiException>(() => client.GetPublishedPageAsync(0, 20, CancellationToken.None));

        Assert.Contains("502", exception.Message);
        Assert.DoesNotContain(Token, exception.Message);
    }

    [Fact]
    public async Task Probe_reports_each_step_independently_and_never_prints_secrets()
    {
        var (client, _) = Create(request => request.RequestUri!.AbsolutePath switch
        {
            "/cgi-bin/stable_token" => FakeHandler.Json(TokenJson),
            "/cgi-bin/freepublish/batchget" => FakeHandler.Json("{\"errcode\":48001,\"errmsg\":\"api unauthorized\"}"),
            "/cgi-bin/material/get_materialcount" => FakeHandler.Json("{\"voice_count\":0,\"video_count\":0,\"image_count\":9,\"news_count\":7}"),
            "/cgi-bin/material/batchget_material" => FakeHandler.Json("{\"errcode\":50001,\"errmsg\":\"user unauthorized\"}"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var steps = await WeChatProbe.RunAsync(client, Options, CancellationToken.None);

        Assert.Equal(5, steps.Count); // 配置 + 4 个接口
        Assert.True(steps.Single(s => s.Name.StartsWith("access_token")).Ok);
        var freepublish = steps.Single(s => s.Name.StartsWith("freepublish"));
        Assert.False(freepublish.Ok);
        Assert.Contains("48001", freepublish.Detail);
        var count = steps.Single(s => s.Name.StartsWith("material/get_materialcount"));
        Assert.True(count.Ok);
        Assert.Equal(7, count.ItemCount);
        Assert.False(steps.Single(s => s.Name.StartsWith("material/batchget_material")).Ok);

        var text = string.Join('\n', steps.Select(s => s.Name + s.Detail));
        Assert.DoesNotContain(Secret, text);
        Assert.DoesNotContain(Token, text);
        Assert.DoesNotContain(Options.AppId, text); // AppId 只显示打码形式
    }

    [Fact]
    public async Task Probe_without_credentials_or_with_bad_token_skips_the_rest()
    {
        var noCreds = await WeChatProbe.RunAsync(
            new WeChatApiClient(new FakeHttpClientFactory(new FakeHandler(_ => throw new InvalidOperationException("must not call"))), WeChatTestSupport.Opts(new WeChatOptions())),
            new WeChatOptions(),
            CancellationToken.None);
        Assert.Single(noCreds);
        Assert.False(noCreds[0].Ok);

        var (client, handler) = Create(_ => FakeHandler.Json("{\"errcode\":40013,\"errmsg\":\"invalid appid\"}"));
        var badToken = await WeChatProbe.RunAsync(client, Options, CancellationToken.None);
        Assert.Contains(badToken, s => s.Name == "后续探测" && !s.Ok);
        Assert.Single(handler.Requests); // 只调了 token
    }
}
