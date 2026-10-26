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
/// 首轮若同步记录表为空则做一次全量回填（补齐历史）；之后每轮只看最新两页。
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

            var firstRun = true;
            using var timer = new PeriodicTimer(options.SyncInterval);
            do
            {
                await RunOnceAsync(firstRun, stoppingToken);
                firstRun = false;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }

    internal async Task RunOnceAsync(bool allowBackfill, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWeChatSyncService>();

            // 同步记录表还没建：每轮都会走到这里，安静跳过。
            if (!await service.IsTableAvailableAsync(cancellationToken))
            {
                _logger.LogWarning("公众号定时同步：{Message}", WeChatSchema.MissingTableMessage);
                return;
            }

            var backfill = false;
            if (allowBackfill)
            {
                var repository = scope.ServiceProvider.GetRequiredService<IWeChatSyncAdminRepository>();
                backfill = (await repository.GetPagedAsync(1, 1, cancellationToken)).Page.TotalCount == 0;
            }

            var summary = await service.SyncFromApiAsync(backfill, SystemActor, cancellationToken);
            if (summary.Error is not null)
            {
                _logger.LogWarning("公众号定时同步未完成：{Error}", summary.Error);
            }
            else
            {
                _logger.LogInformation(
                    "公众号定时同步完成（{Mode}）：拉取 {Fetched}，新增 {Created}，更新 {Updated}，无变化 {Unchanged}，失败 {Failed}。",
                    backfill ? "全量回填" : "增量", summary.Fetched, summary.Created, summary.Updated, summary.Unchanged, summary.Failed);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "公众号定时同步本轮出错，下一轮重试。");
        }
    }
}
