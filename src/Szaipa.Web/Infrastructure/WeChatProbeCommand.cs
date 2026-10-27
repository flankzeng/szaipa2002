using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// <c>dotnet run --project src/Szaipa.Web -- wechat-probe</c>：探测公众号官方接口的可用性并退出（不启动站点）。
/// 使用与站点相同的<b>有效配置</b>：配置文件（Local.json / user-secrets / 环境变量）再被后台设置页的加密存储覆盖；
/// 因此需要与站点一致的 Data Protection 配置（由 Program 传入）才能解密存储里的 AppSecret。
/// 输出不含 AppSecret 与 access_token。退出码：0 = token 可用（其余接口各自的可用性见输出），1 = 无凭据或 token 不可用。
/// </summary>
internal static class WeChatProbeCommand
{
    public static async Task<int> RunAsync(
        IConfiguration configuration,
        WeChatCredentialStoreLocation storeLocation,
        Action<IServiceCollection> configureDataProtection,
        TextWriter? output = null,
        HttpMessageHandler? handler = null,
        string[]? args = null)
    {
        output ??= Console.Out;

        var services = new ServiceCollection();
        configureDataProtection(services);
        services.AddWeChatEffectiveOptions(configuration, _ => storeLocation);
        var http = services.AddHttpClient(WeChatApiClient.ClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        if (handler is not null)
        {
            http.ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        services.AddSingleton<IWeChatApiClient, WeChatApiClient>();
        await using var provider = services.BuildServiceProvider();

        await output.WriteLineAsync("公众号接口能力探测（只读，不输出密钥/token）");
        var store = provider.GetRequiredService<IWeChatCredentialStore>();
        var stored = store.Current;
        if (!store.Location.IsAvailable)
        {
            await output.WriteLineAsync($"  [提示] 后台设置页存储不可用：{store.Location.Problem} 仅使用配置文件的值。");
        }
        else if (stored.Exists)
        {
            await output.WriteLineAsync("  [提示] 已叠加后台设置页保存的配置" + (stored.LoadError is null ? "。" : $"（{stored.LoadError}）"));
            if (stored.AppSecretUnreadable)
            {
                await output.WriteLineAsync("  [提示] 已保存的 AppSecret 无法解密（Data Protection 密钥可能变化），请在后台重新录入。");
            }
        }

        var options = provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>().CurrentValue;
        if (args is not null && args.Contains("wechat-history-probe", StringComparer.OrdinalIgnoreCase))
        {
            return await RunHistoryAsync(provider.GetRequiredService<IWeChatApiClient>(), options, args, output);
        }

        var steps = await WeChatProbe.RunAsync(provider.GetRequiredService<IWeChatApiClient>(), options, CancellationToken.None);
        foreach (var step in steps)
        {
            await output.WriteLineAsync($"  [{(step.Ok ? "可用" : "不可用")}] {step.Name}：{step.Detail}");
        }

        var tokenOk = steps.Any(s => s.Name.StartsWith("access_token", StringComparison.Ordinal) && s.Ok);
        await output.WriteLineAsync(tokenOk
            ? "结论：token 可用。上面标「不可用」的接口请在后台「接口配置」页关闭对应来源，或改用链接导入。"
            : "结论：未能取得 token，接口来源不可用；链接导入不受影响。");
        return tokenOk ? 0 : 1;
    }

    /// <summary>
    /// <c>wechat-history-probe [--scan-from yyyy-MM-dd] [--out 文件.json]</c>：逐个核查可能列出历史文章的只读接口
    /// （见 <see cref="WeChatHistoryProbe"/>），控制台只打印各接口结果与条目数；标题/链接清单写入 <c>--out</c> 指定的文件。
    /// </summary>
    private static async Task<int> RunHistoryAsync(IWeChatApiClient client, WeChatOptions options, string[] args, TextWriter output)
    {
        if (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret))
        {
            await output.WriteLineAsync("未找到 AppID / AppSecret，无法核查。");
            return 1;
        }

        DateOnly? scanFrom = ArgumentValue(args, "--scan-from") is { } from
            ? DateOnly.ParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var result = await WeChatHistoryProbe.RunAsync(client, today, scanFrom, CancellationToken.None);

        await output.WriteLineAsync("公众号历史文章途径核查（只读，不输出密钥/token）");
        foreach (var step in result.Steps)
        {
            var code = step.ErrorCode is { } errorCode ? $"[errcode {errorCode}] " : string.Empty;
            await output.WriteLineAsync($"  [{(step.Ok ? "可用" : "不可用")}] {step.Name}：{code}{step.Detail}");
        }

        foreach (var group in result.Items.GroupBy(i => i.Source))
        {
            await output.WriteLineAsync($"  来源 {group.Key}：{group.Count()} 条，其中带链接 {group.Count(i => !string.IsNullOrEmpty(i.Url))} 条");
        }

        if (ArgumentValue(args, "--out") is { } outPath)
        {
            await File.WriteAllTextAsync(outPath, System.Text.Json.JsonSerializer.Serialize(result.Items,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
            await output.WriteLineAsync($"  清单已写入 {outPath}");
        }

        return result.Steps.Any(s => s.Ok) ? 0 : 1;
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
