using Microsoft.Extensions.DependencyInjection;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;

namespace Szaipa.Web.Infrastructure;

/// <summary>
/// <c>dotnet run --project src/Szaipa.Web -- wechat-probe</c>：探测公众号官方接口的可用性并退出（不启动站点）。
/// 凭据只从已装配的配置（Local.json / user-secrets / 环境变量）读取；输出不含 AppSecret 与 access_token。
/// 退出码：0 = token 可用（其余接口各自的可用性见输出），1 = 无凭据或 token 不可用。
/// </summary>
internal static class WeChatProbeCommand
{
    public static async Task<int> RunAsync(IConfiguration configuration, TextWriter? output = null, HttpMessageHandler? handler = null)
    {
        output ??= Console.Out;

        var options = new WeChatOptions();
        configuration.GetSection(WeChatOptions.SectionName).Bind(options);

        var services = new ServiceCollection();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        var http = services.AddHttpClient(WeChatApiClient.ClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        if (handler is not null)
        {
            http.ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        services.AddSingleton<IWeChatApiClient, WeChatApiClient>();
        await using var provider = services.BuildServiceProvider();

        await output.WriteLineAsync("公众号接口能力探测（只读，不输出密钥/token）");
        var steps = await WeChatProbe.RunAsync(provider.GetRequiredService<IWeChatApiClient>(), options, CancellationToken.None);
        foreach (var step in steps)
        {
            await output.WriteLineAsync($"  [{(step.Ok ? "可用" : "不可用")}] {step.Name}：{step.Detail}");
        }

        var tokenOk = steps.Any(s => s.Name.StartsWith("access_token", StringComparison.Ordinal) && s.Ok);
        await output.WriteLineAsync(tokenOk
            ? "结论：token 可用。上面标「不可用」的接口请在后台关闭对应来源（WeChat:Sources:*=false）或改用链接导入。"
            : "结论：未能取得 token，接口来源不可用；链接导入不受影响。");
        return tokenOk ? 0 : 1;
    }
}
