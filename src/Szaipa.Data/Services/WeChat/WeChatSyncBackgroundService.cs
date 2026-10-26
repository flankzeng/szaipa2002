using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 定时增量同步公众号新发布的文章。仅当 <c>WeChat:Enabled</c> 且 AppId/AppSecret 齐全、并且后台写库
/// （<c>AdminWrite</c>）已配置时才工作；否则启动即静默退出，不影响站点。每轮失败只记日志，下一轮重试，永不崩溃。
/// 每次进程启动后的 API 首轮都完整发现历史；失败时下一轮继续全量，成功后才切换为增量。全量依靠来源键去重，不覆盖既有文章。
/// </summary>
public sealed class WeChatSyncBackgroundService : BackgroundService
{
    private static readonly AdminActor SystemActor = new(0, "公众号同步");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WeChatOptions> _options;
    private readonly IOptions<AdminWriteOptions> _adminOptions;
    private readonly ILogger<WeChatSyncBackgroundService> _logger;

    public WeChatSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<WeChatOptions> options,
        IOptions<AdminWriteOptions> adminOptions,
        ILogger<WeChatSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _adminOptions = adminOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.IsApiConfigured)
        {
            _logger.LogInformation("公众号定时同步未启用（WeChat:Enabled / AppId / AppSecret 未配置齐全）。");
            return;
        }

        if (!_adminOptions.Value.IsConfigured)
        {
            _logger.LogWarning("公众号定时同步已配置，但后台写库（AdminWrite）未配置，无处落库，已跳过。");
            return;
        }

        try
        {
            // 启动后稍等，让站点先完成预热。
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            var backfillState = new WeChatApiBackfillState();
            using var timer = new PeriodicTimer(options.SyncInterval);
            do
            {
                var succeeded = await RunOnceAsync(backfillState.NeedsBackfill, stoppingToken);
                backfillState.RecordResult(succeeded);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    internal async Task<bool> RunOnceAsync(bool full, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWeChatSyncService>();

            // 同步记录表还没建：每轮都会走到这里，安静跳过。
            if (!await service.IsTableAvailableAsync(cancellationToken))
            {
                _logger.LogWarning("公众号定时同步：{Message}", WeChatSchema.MissingTableMessage);
                return false;
            }

            var summary = await service.SyncFromApiAsync(full, SystemActor, cancellationToken);
            var succeeded = summary.Error is null
                            && summary.Failed == 0
                            && summary.Sources.Count > 0
                            && summary.Sources.All(source => source.Error is null);
            if (!succeeded)
            {
                _logger.LogWarning(
                    "公众号定时同步未完成（{Mode}）：{Error}，失败 {Failed}。",
                    full ? "全量回填" : "增量",
                    summary.Error ?? string.Join("；", summary.Sources.Where(source => source.Error is not null).Select(source => source.Error)),
                    summary.Failed);
            }
            else
            {
                _logger.LogInformation(
                    "公众号定时同步完成（{Mode}）：拉取 {Fetched}，新增 {Created}，更新 {Updated}，无变化 {Unchanged}，失败 {Failed}。",
                    full ? "全量回填" : "增量", summary.Fetched, summary.Created, summary.Updated, summary.Unchanged, summary.Failed);
            }

            return succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "公众号定时同步本轮出错，下一轮重试。");
            return false;
        }
    }
}

/// <summary>每次进程启动都从全量发现开始；不持久化断点，已有来源键由同步器跳过。</summary>
internal sealed class WeChatApiBackfillState
{
    public bool NeedsBackfill { get; private set; } = true;

    public void RecordResult(bool succeeded)
    {
        if (NeedsBackfill && succeeded)
        {
            NeedsBackfill = false;
        }
    }
}
