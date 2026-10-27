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
        HttpMessageHandler? handler = null)
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
}
