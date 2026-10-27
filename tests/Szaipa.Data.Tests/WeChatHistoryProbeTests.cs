using System.Text.Json.Nodes;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class WeChatHistoryProbeTests
{
    [Fact]
    public async Task ReportsEachEndpointAndCollectsArticles()
    {
        var api = new ScriptedApi();
        api.Responses["/cgi-bin/material/batchget_material"] = JsonNode.Parse("""
            {"total_count":2,"item_count":2,"item":[
              {"media_id":"m1","update_time":1700000000,"content":{"news_item":[
                {"title":"展览开幕","url":"https://mp.weixin.qq.com/s?__biz=X&mid=1&idx=1&sn=a&chksm=b"},
                {"title":"已删","url":"https://mp.weixin.qq.com/s?__biz=X&mid=1&idx=2&sn=c","is_deleted":true}]}},
              {"media_id":"m2","update_time":1700000100,"content":{"news_item":[{"title":"讲座","url":""}]}}]}
            """)!;
        api.Responses["/cgi-bin/get_current_selfmenu_info"] = JsonNode.Parse("""
            {"is_menu_open":1,"selfmenu_info":{"button":[{"type":"view","url":"https://mp.weixin.qq.com/s/abcDEF"}]}}
            """)!;
        api.Responses["/datacube/getarticlesummary"] = JsonNode.Parse("""
            {"list":[{"ref_date":"2026-10-26","msgid":"100_1","title":"群发一"},{"ref_date":"2026-10-26","msgid":"100_1","title":"群发一"}]}
            """)!;

        var result = await WeChatHistoryProbe.RunAsync(api, new DateOnly(2026, 10, 28), scanFrom: new DateOnly(2026, 10, 25), CancellationToken.None);

        var freePublish = Assert.Single(result.Steps, s => s.Name.StartsWith("freepublish", StringComparison.Ordinal));
        Assert.False(freePublish.Ok);
        Assert.Equal(48001, freePublish.ErrorCode);
        Assert.True(Assert.Single(result.Steps, s => s.Name.StartsWith("material", StringComparison.Ordinal)).Ok);

        Assert.Contains(result.Items, i => i.Source == "material" && i.Title == "展览开幕" && i.Date == "2023-11-15");
        Assert.Contains(result.Items, i => i.Source == "material:deleted" && i.Title == "已删");
        Assert.Contains(result.Items, i => i.Source == "menu" && i.Url == "https://mp.weixin.qq.com/s/abcDEF");
        Assert.Single(result.Items, i => i.Source == "datacube" && i.MessageId == "100_1");
        // 逐日扫描到前天（2026-10-25、10-26），加首次的昨天，共 3 次统计调用。
        Assert.Equal(3, api.Calls.Count(c => c == "/datacube/getarticlesummary"));
    }

    [Fact]
    public async Task ClientRejectsPathsOutsideReadOnlyAllowlist()
    {
        var client = new WeChatApiClient(new NoHttp(), new StaticOptions());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.QueryReadOnlyAsync("/cgi-bin/freepublish/submit", new JsonObject(), CancellationToken.None));
        Assert.DoesNotContain(WeChatApiClient.ReadOnlyQueryPaths, p => p.Contains("submit") || p.Contains("delete") || p.Contains("send"));
    }

    private sealed class ScriptedApi : IWeChatApiClient
    {
        public Dictionary<string, JsonNode> Responses { get; } = new();
        public List<string> Calls { get; } = new();

        public Task VerifyCredentialsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WeChatSourcePage> GetPublishedPageAsync(int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WeChatSourcePage> GetMaterialNewsPageAsync(int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WeChatMaterialCount> GetMaterialCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<JsonNode> QueryReadOnlyAsync(string path, JsonNode? body, CancellationToken cancellationToken)
        {
            Calls.Add(path);
            return Responses.TryGetValue(path, out var json)
                ? Task.FromResult(json.DeepClone())
                : Task.FromException<JsonNode>(new WeChatApiException("api unauthorized", 48001));
        }
    }

    private sealed class NoHttp : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("不应发出请求");
    }

    private sealed class StaticOptions : Microsoft.Extensions.Options.IOptionsMonitor<Szaipa.Data.Configuration.WeChatOptions>
    {
        public Szaipa.Data.Configuration.WeChatOptions CurrentValue { get; } = new();
        public Szaipa.Data.Configuration.WeChatOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<Szaipa.Data.Configuration.WeChatOptions, string?> listener) => null;
    }
}
