using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 定时增量同步公众号新发布的文章。仅当有效配置 <c>WeChat:Enabled</c> 且 AppId/AppSecret 齐全、并且后台写库
/// （<c>AdminWrite</c>）已配置时才工作；每轮失败只记日志，下一轮重试，永不崩溃。
/// 每次进程启动后的 API 首轮都完整发现历史；失败时下一轮继续全量，成功后才切换为增量。全量依靠来源键去重，不覆盖既有文章。
/// <para>
/// 配置来自 <see cref="IOptionsMonitor{TOptions}"/>（配置文件 + 后台设置页覆盖），无需重启即生效：
/// 未启用时挂起等待配置变化；从关到开、或 AppId/AppSecret 变化时立即跑一轮（凭据变化重新全量发现）；
/// 只改间隔时按新间隔计算下一轮；开着时被关掉，下一轮就不再同步。
/// </para>
/// </summary>
public sealed class WeChatSyncBackgroundService : BackgroundService
{
    private static readonly AdminActor SystemActor = new(0, "公众号同步");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WeChatOptions> _options;
    private readonly IOptions<AdminWriteOptions> _adminOptions;
    private readonly ILogger<WeChatSyncBackgroundService> _logger;
    private TaskCompletionSource _changed = NewSignal();

    public WeChatSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<WeChatOptions> options,
        IOptions<AdminWriteOptions> adminOptions,
        ILogger<WeChatSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _adminOptions = adminOptions;
        _logger = logger;
    }

    /// <summary>站点启动后首轮同步前的预热等待；测试里缩短。</summary>
    internal TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>测试钩子：替换真实的单轮同步（参数为 full）。</summary>
    internal Func<bool, CancellationToken, Task<bool>>? RunOnceOverride { get; init; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = _options.OnChange(_ => Interlocked.Exchange(ref _changed, NewSignal()).TrySetResult());

        var backfillState = new WeChatApiBackfillState();
        var warmedUp = false;
        var loggedDisabled = false;
        string? lastFingerprint = null;
        DateTime? lastRunUtc = null;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 先拿信号再读配置：读完之后发生的变更一定能唤醒下面的等待。
                var changed = Volatile.Read(ref _changed).Task;
                var options = _options.CurrentValue;

                if (!options.IsApiConfigured)
                {
                    if (!loggedDisabled)
                    {
                        _logger.LogInformation("公众号定时同步未启用（WeChat:Enabled / AppId / AppSecret 未配置齐全），等待后台配置。");
                        loggedDisabled = true;
                    }

                    lastRunUtc = null; // 再次开启时立即跑一轮
                    await WaitAsync(changed, Timeout.InfiniteTimeSpan, stoppingToken);
                    continue;
                }

                loggedDisabled = false;
                if (!_adminOptions.Value.IsConfigured)
                {
                    _logger.LogWarning("公众号定时同步已配置，但后台写库（AdminWrite）未配置，无处落库，已跳过。");
                    return;
                }

                if (!warmedUp)
                {
                    // 启动后稍等，让站点先完成预热。
                    warmedUp = true;
                    await Task.Delay(StartupDelay, stoppingToken);
                    continue; // 预热期间配置可能又变了，重新读取
                }

                var fingerprint = WeChatSecretMask.Fingerprint(options);
                var credentialsChanged = lastFingerprint is not null && !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal);
                if (credentialsChanged)
                {
                    backfillState = new WeChatApiBackfillState(); // 换了公众号/凭据：重新完整发现
                }

                var dueUtc = lastRunUtc + options.SyncInterval;
                if (lastRunUtc is null || credentialsChanged || DateTime.UtcNow >= dueUtc)
                {
                    lastFingerprint = fingerprint;
                    var succeeded = RunOnceOverride is null
                        ? await RunOnceAsync(backfillState.NeedsBackfill, stoppingToken)
                        : await RunOnceOverride(backfillState.NeedsBackfill, stoppingToken);
                    backfillState.RecordResult(succeeded);
                    lastRunUtc = DateTime.UtcNow;
                    dueUtc = lastRunUtc + options.SyncInterval;
                }

                var wait = dueUtc!.Value - DateTime.UtcNow;
                await WaitAsync(changed, wait > TimeSpan.Zero ? wait : TimeSpan.Zero, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停机
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>等到间隔到期或配置变化（先到者）。</summary>
    private static async Task WaitAsync(Task changed, TimeSpan timeout, CancellationToken stoppingToken)
    {
        if (changed.IsCompleted)
        {
            return;
        }

        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var delay = Task.Delay(timeout, delayCancellation.Token);
        await Task.WhenAny(changed, delay);
        delayCancellation.Cancel();
        stoppingToken.ThrowIfCancellationRequested();
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
