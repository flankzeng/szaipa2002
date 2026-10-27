using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Szaipa.Data.Services.WeChat;

/// <summary>历史文章途径核查里找到的一篇文章（任一接口给出的标题/链接/日期，缺的字段为 null）。</summary>
public sealed record WeChatHistoryItem(string Source, string? Title, string? Url, string? Date, string? MessageId);

/// <summary>历史文章途径核查的结果：每个接口一步，外加各接口找到的文章清单。</summary>
public sealed record WeChatHistoryProbeResult(IReadOnlyList<WeChatProbeStep> Steps, IReadOnlyList<WeChatHistoryItem> Items);

/// <summary>
/// 公众号历史文章途径核查（<c>wechat-history-probe</c>）：用本账号实际权限逐个调用可能列出历史文章的<b>只读</b>接口——
/// 发布记录、草稿箱、永久图文素材、自定义菜单、自动回复、数据统计（图文群发每日数据）——
/// 报告每个接口「可用 / 错误码」并汇总能拿到的标题、链接、群发 msgid。
/// 不发布、不群发、不删除、不改任何公众号内容；输出不含 AppSecret / access_token。
/// <paramref name="scanFrom"/> 有值且图文统计接口可用时，逐日扫描到昨天（每天一次调用），得到群发标题清单。
/// </summary>
public static class WeChatHistoryProbe
{
    private static readonly Regex ArticleUrlPattern = new(
        @"https?://mp\.weixin\.qq\.com/s[^\s""'<>\\]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static async Task<WeChatHistoryProbeResult> RunAsync(
        IWeChatApiClient client, DateOnly today, DateOnly? scanFrom, CancellationToken cancellationToken)
    {
        var steps = new List<WeChatProbeStep>();
        var items = new List<WeChatHistoryItem>();
        var yesterday = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await StepAsync(steps, "freepublish/batchget（发布记录）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/cgi-bin/freepublish/batchget",
                new JsonObject { ["offset"] = 0, ["count"] = 20, ["no_content"] = 1 }, cancellationToken);
            CollectNewsItems(json, "freepublish", items);
            return $"可用，total_count={json["total_count"]}";
        });

        await StepAsync(steps, "draft/count（草稿数量）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/cgi-bin/draft/count", null, cancellationToken);
            return $"可用，total_count={json["total_count"]}";
        });

        await StepAsync(steps, "draft/batchget（草稿箱列表）", async () =>
        {
            var total = await PageAllAsync(client, "/cgi-bin/draft/batchget",
                offset => new JsonObject { ["offset"] = offset, ["count"] = 20, ["no_content"] = 1 }, "draft", items, cancellationToken);
            return $"可用，total_count={total}";
        });

        await StepAsync(steps, "material/batchget_material（永久图文素材）", async () =>
        {
            var total = await PageAllAsync(client, "/cgi-bin/material/batchget_material",
                offset => new JsonObject { ["type"] = "news", ["offset"] = offset, ["count"] = 20 }, "material", items, cancellationToken);
            return $"可用，total_count={total}";
        });

        await StepAsync(steps, "get_current_selfmenu_info（自定义菜单）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/cgi-bin/get_current_selfmenu_info", null, cancellationToken);
            return $"可用，菜单内文章链接 {CollectUrls(json, "menu", items)} 条";
        });

        await StepAsync(steps, "get_current_autoreply_info（自动回复）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/cgi-bin/get_current_autoreply_info", null, cancellationToken);
            return $"可用，自动回复内文章链接 {CollectUrls(json, "autoreply", items)} 条";
        });

        var dateRange = new JsonObject { ["begin_date"] = yesterday, ["end_date"] = yesterday };
        await StepAsync(steps, "datacube/getusersummary（用户统计，作数据统计权限对照）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/datacube/getusersummary", dateRange.DeepClone(), cancellationToken);
            return $"可用，{yesterday} 记录 {json["list"]?.AsArray().Count ?? 0} 条";
        });

        await StepAsync(steps, "datacube/getuserread（图文统计概况）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/datacube/getuserread", dateRange.DeepClone(), cancellationToken);
            return $"可用，{yesterday} 记录 {json["list"]?.AsArray().Count ?? 0} 条";
        });

        var summaryOk = await StepAsync(steps, "datacube/getarticlesummary（图文群发每日数据）", async () =>
        {
            var json = await client.QueryReadOnlyAsync("/datacube/getarticlesummary", dateRange.DeepClone(), cancellationToken);
            return $"可用，{yesterday} 被阅读的群发图文 {CollectSummary(json, items)} 篇";
        });

        if (summaryOk && scanFrom is { } from)
        {
            await StepAsync(steps, $"逐日扫描 getarticlesummary（{from:yyyy-MM-dd} 起）", async () =>
            {
                var days = 0;
                for (var day = from; day < today.AddDays(-1); day = day.AddDays(1))
                {
                    var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var json = await client.QueryReadOnlyAsync("/datacube/getarticlesummary",
                        new JsonObject { ["begin_date"] = date, ["end_date"] = date }, cancellationToken);
                    CollectSummary(json, items);
                    days++;
                }

                return $"完成，扫描 {days} 天";
            });
        }

        return new WeChatHistoryProbeResult(steps, items);
    }

    private static async Task<int> PageAllAsync(
        IWeChatApiClient client, string path, Func<int, JsonObject> body, string source,
        List<WeChatHistoryItem> items, CancellationToken cancellationToken)
    {
        var total = 0;
        for (var offset = 0; offset < 2000; offset += 20)
        {
            var json = await client.QueryReadOnlyAsync(path, body(offset), cancellationToken);
            total = json["total_count"]?.GetValue<int>() ?? 0;
            var count = json["item_count"]?.GetValue<int>() ?? 0;
            CollectNewsItems(json, source, items);
            if (count == 0 || offset + count >= total)
            {
                break;
            }
        }

        return total;
    }

    private static void CollectNewsItems(JsonNode json, string source, List<WeChatHistoryItem> items)
    {
        foreach (var item in json["item"]?.AsArray() ?? new JsonArray())
        {
            var seconds = item?["update_time"]?.GetValue<long>();
            var date = seconds is > 946684800
                ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
            foreach (var news in item?["content"]?["news_item"]?.AsArray() ?? new JsonArray())
            {
                var deleted = news?["is_deleted"]?.GetValue<bool>() == true;
                items.Add(new WeChatHistoryItem(
                    deleted ? source + ":deleted" : source,
                    news?["title"]?.GetValue<string>()?.Trim(),
                    news?["url"]?.GetValue<string>()?.Trim(),
                    date,
                    null));
            }
        }
    }

    private static int CollectUrls(JsonNode json, string source, List<WeChatHistoryItem> items)
    {
        var urls = ArticleUrlPattern.Matches(json.ToJsonString())
            .Select(m => m.Value.Replace("\\u0026", "&", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        items.AddRange(urls.Select(url => new WeChatHistoryItem(source, null, url, null, null)));
        return urls.Count;
    }

    private static int CollectSummary(JsonNode json, List<WeChatHistoryItem> items)
    {
        var count = 0;
        foreach (var row in json["list"]?.AsArray() ?? new JsonArray())
        {
            var messageId = row?["msgid"]?.GetValue<string>();
            if (items.Any(i => i.Source == "datacube" && i.MessageId == messageId))
            {
                continue; // 同一篇在多天被阅读，只记首次出现（≈群发日）
            }

            items.Add(new WeChatHistoryItem("datacube", row?["title"]?.GetValue<string>()?.Trim(), null,
                row?["ref_date"]?.GetValue<string>(), messageId));
            count++;
        }

        return count;
    }

    private static async Task<bool> StepAsync(List<WeChatProbeStep> steps, string name, Func<Task<string>> run)
    {
        try
        {
            steps.Add(new WeChatProbeStep(name, true, await run(), null));
            return true;
        }
        catch (WeChatApiException exception)
        {
            steps.Add(new WeChatProbeStep(name, false, exception.Message, null, exception.ErrorCode, exception.CallerIp));
            return false;
        }
    }
}
