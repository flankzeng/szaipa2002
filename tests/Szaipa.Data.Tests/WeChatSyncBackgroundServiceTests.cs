using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class WeChatSyncBackgroundServiceTests
{
    [Fact]
    public async Task First_run_is_full_even_when_sync_table_has_existing_records()
    {
        using var provider = CreateProvider(new FakeSyncService
        {
            TableAvailable = true,
            Summary = SuccessfulSummary()
        });
        var service = CreateBackgroundService(provider.GetRequiredService<IServiceScopeFactory>());
        var state = new WeChatApiBackfillState();

        var succeeded = await service.RunOnceAsync(state.NeedsBackfill, CancellationToken.None);
        state.RecordResult(succeeded);
        var nextSucceeded = await service.RunOnceAsync(state.NeedsBackfill, CancellationToken.None);

        Assert.True(nextSucceeded);
        Assert.Equal(new[] { true, false }, ((FakeSyncService)provider.GetRequiredService<IWeChatSyncService>()).FullArguments);
        Assert.False(state.NeedsBackfill);
    }

    [Theory]
    [MemberData(nameof(IncompleteSummaries))]
    public async Task Failed_full_run_keeps_backfill_enabled_for_the_next_cycle(WeChatSyncSummary summary)
    {
        using var provider = CreateProvider(new FakeSyncService { TableAvailable = true, Summary = summary });
        var service = CreateBackgroundService(provider.GetRequiredService<IServiceScopeFactory>());
        var state = new WeChatApiBackfillState();

        var firstSucceeded = await service.RunOnceAsync(state.NeedsBackfill, CancellationToken.None);
        state.RecordResult(firstSucceeded);
        var secondSucceeded = await service.RunOnceAsync(state.NeedsBackfill, CancellationToken.None);
        state.RecordResult(secondSucceeded);

        Assert.False(firstSucceeded);
        Assert.False(secondSucceeded);
        Assert.True(state.NeedsBackfill);
        Assert.Equal(new[] { true, true }, ((FakeSyncService)provider.GetRequiredService<IWeChatSyncService>()).FullArguments);
    }

    [Fact]
    public async Task Missing_sync_table_keeps_backfill_enabled()
    {
        using var provider = CreateProvider(new FakeSyncService { TableAvailable = false });
        var service = CreateBackgroundService(provider.GetRequiredService<IServiceScopeFactory>());
        var state = new WeChatApiBackfillState();

        var succeeded = await service.RunOnceAsync(state.NeedsBackfill, CancellationToken.None);
        state.RecordResult(succeeded);

        Assert.False(succeeded);
        Assert.True(state.NeedsBackfill);
        Assert.Empty(((FakeSyncService)provider.GetRequiredService<IWeChatSyncService>()).FullArguments);
    }

    public static IEnumerable<object[]> IncompleteSummaries()
    {
        yield return [WeChatSyncSummary.Failure("来源接口失败")];
        yield return [new WeChatSyncSummary(
            [new WeChatSourceRunResult(WeChatSources.FreePublish, 10, 8, 0, 1, 1, null)], null)];
        yield return [new WeChatSyncSummary(
            [new WeChatSourceRunResult(WeChatSources.FreePublish, 0, 0, 0, 0, 0, "来源接口失败")], null)];
    }

    private static WeChatSyncSummary SuccessfulSummary() => new(
        [new WeChatSourceRunResult(WeChatSources.FreePublish, 10, 0, 0, 10, 0, null)], null);

    private static ServiceProvider CreateProvider(FakeSyncService syncService) => new ServiceCollection()
        .AddSingleton<IWeChatSyncService>(syncService)
        .BuildServiceProvider();

    private static WeChatSyncBackgroundService CreateBackgroundService(IServiceScopeFactory scopeFactory) => new(
        scopeFactory,
        Options.Create(new WeChatOptions()),
        Options.Create(new AdminWriteOptions()),
        NullLogger<WeChatSyncBackgroundService>.Instance);

    private sealed class FakeSyncService : IWeChatSyncService
    {
        public bool TableAvailable { get; init; }
        public WeChatSyncSummary Summary { get; init; } = SuccessfulSummary();
        public List<bool> FullArguments { get; } = [];

        public Task<bool> IsTableAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(TableAvailable);

        public Task<IReadOnlyList<WeChatImportOutcome>> ImportLinksAsync(
            string rawInput, AdminActor actor, bool force, CancellationToken cancellationToken,
            string source = WeChatSources.Link) => throw new NotSupportedException();

        public Task<WeChatImportOutcome> ReimportAsync(int recordId, AdminActor actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WeChatSyncSummary> SyncFromApiAsync(bool full, AdminActor actor, CancellationToken cancellationToken)
        {
            FullArguments.Add(full);
            return Task.FromResult(Summary);
        }

        public Task<WeChatImportOutcome> ImportArticleAsync(
            WeChatArticle article, AdminActor actor, bool force, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
