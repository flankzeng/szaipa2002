using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>能力探测的一步结果。</summary>
public sealed record WeChatProbeStep(string Name, bool Ok, string Detail, int? ItemCount);

/// <summary>
/// 公众号接口能力探测（<c>dotnet run --project src/Szaipa.Web -- wechat-probe</c>）。依次探测：
/// token → freepublish/batchget(count=1) → material/get_materialcount → batchget_material(news, count=1)，
/// 每一步独立报告「可用 / 错误码+含义」与可拿到的条目数。<b>只读；输出里永远不含 AppSecret / access_token。</b>
/// 未认证号常见：token 通过，freepublish/material 返回 48001/50001。
/// </summary>
public static class WeChatProbe
{
    public static async Task<IReadOnlyList<WeChatProbeStep>> RunAsync(
        IWeChatApiClient client, WeChatOptions options, CancellationToken cancellationToken)
    {
        var steps = new List<WeChatProbeStep>();

        if (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret))
        {
            steps.Add(new WeChatProbeStep("配置", false, "未找到 WeChat:AppId / WeChat:AppSecret（请放在 appsettings.Local.json、user-secrets 或环境变量 WeChat__AppId / WeChat__AppSecret）。", null));
            return steps;
        }

        steps.Add(new WeChatProbeStep("配置", true, $"已读取 AppId（{Mask(options.AppId)}）与 AppSecret（已隐藏）。", null));

        var tokenOk = await StepAsync(steps, "access_token（stable_token）", async () =>
        {
            await client.VerifyCredentialsAsync(cancellationToken);
            return ("可用", (int?)null);
        });
        if (!tokenOk)
        {
            steps.Add(new WeChatProbeStep("后续探测", false, "token 不可用，已跳过其余接口。", null));
            return steps;
        }

        await StepAsync(steps, "freepublish/batchget（发布记录）", async () =>
        {
            var page = await client.GetPublishedPageAsync(0, 1, cancellationToken);
            return ($"可用，共 {page.TotalCount} 次发布记录", (int?)page.TotalCount);
        });

        await StepAsync(steps, "material/get_materialcount（素材数量）", async () =>
        {
            var count = await client.GetMaterialCountAsync(cancellationToken);
            return ($"可用，图文素材 {count.News}、图片 {count.Image}、视频 {count.Video}、语音 {count.Voice}", (int?)count.News);
        });

        await StepAsync(steps, "material/batchget_material（永久图文素材 news）", async () =>
        {
            var page = await client.GetMaterialNewsPageAsync(0, 1, cancellationToken);
            return ($"可用，共 {page.TotalCount} 条图文素材", (int?)page.TotalCount);
        });

        return steps;
    }

    private static async Task<bool> StepAsync(List<WeChatProbeStep> steps, string name, Func<Task<(string Detail, int? Count)>> run)
    {
        try
        {
            var (detail, count) = await run();
            steps.Add(new WeChatProbeStep(name, true, detail, count));
            return true;
        }
        catch (WeChatApiException exception)
        {
            steps.Add(new WeChatProbeStep(name, false, exception.Message, null));
            return false;
        }
    }

    private static string Mask(string appId) =>
        appId.Length <= 6 ? new string('*', appId.Length) : appId[..4] + new string('*', appId.Length - 6) + appId[^2..];
}
