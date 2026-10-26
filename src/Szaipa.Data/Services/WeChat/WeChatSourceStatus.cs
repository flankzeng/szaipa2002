using System.Collections.Concurrent;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>后台「来源状态」表的一行。</summary>
public sealed record WeChatSourceStatusEntry(
    string SourceId,
    string DisplayName,
    bool Enabled,
    string EnabledNote,
    DateTime? LastSuccessAt,
    DateTime? LastAttemptAt,
    string? LastError,
    string? LastSummary);

/// <summary>
/// 各来源最近一次成功/失败的内存记录（进程级单例，重启后清空——够后台看"现在通不通"，不为此新增数据库表）。
/// 来源彼此独立：一个来源失败只影响它自己的状态行。
/// </summary>
public interface IWeChatSourceStatusStore
{
    void RecordSuccess(string sourceId, string summary);

    void RecordFailure(string sourceId, string error);

    IReadOnlyList<WeChatSourceStatusEntry> Snapshot(WeChatOptions options);
}

/// <inheritdoc />
public sealed class WeChatSourceStatusStore : IWeChatSourceStatusStore
{
    private sealed class State
    {
        public DateTime? LastSuccessAt;
        public DateTime? LastAttemptAt;
        public string? LastError;
        public string? LastSummary;
    }

    private readonly ConcurrentDictionary<string, State> _states = new();

    public void RecordSuccess(string sourceId, string summary)
    {
        var state = _states.GetOrAdd(sourceId, _ => new State());
        lock (state)
        {
            state.LastAttemptAt = state.LastSuccessAt = DateTime.Now;
            state.LastError = null;
            state.LastSummary = summary;
        }
    }

    public void RecordFailure(string sourceId, string error)
    {
        var state = _states.GetOrAdd(sourceId, _ => new State());
        lock (state)
        {
            state.LastAttemptAt = DateTime.Now;
            state.LastError = error;
        }
    }

    public IReadOnlyList<WeChatSourceStatusEntry> Snapshot(WeChatOptions options)
    {
        var rows = new List<WeChatSourceStatusEntry>();
        foreach (var id in new[] { WeChatSources.Link, WeChatSources.FreePublish, WeChatSources.Material, WeChatSources.Callback })
        {
            var (enabled, note) = Describe(id, options);
            _states.TryGetValue(id, out var state);
            if (state is null)
            {
                rows.Add(new WeChatSourceStatusEntry(id, WeChatSources.DisplayName(id), enabled, note, null, null, null, null));
                continue;
            }

            lock (state)
            {
                rows.Add(new WeChatSourceStatusEntry(
                    id, WeChatSources.DisplayName(id), enabled, note,
                    state.LastSuccessAt, state.LastAttemptAt, state.LastError, state.LastSummary));
            }
        }

        return rows;
    }

    private static (bool Enabled, string Note) Describe(string id, WeChatOptions options) => id switch
    {
        WeChatSources.Link => (true, "始终可用（后台粘贴链接）"),
        WeChatSources.FreePublish or WeChatSources.Material when !options.IsApiConfigured =>
            (false, "未启用：WeChat:Enabled / AppId / AppSecret 未配置齐全"),
        WeChatSources.FreePublish => options.Sources.FreePublish
            ? (true, "已启用（定时增量 + 手动全量回填）")
            : (false, "已关闭：WeChat:Sources:FreePublish=false"),
        WeChatSources.Material => options.Sources.Material
            ? (true, "已启用（定时增量 + 手动全量回填）")
            : (false, "已关闭：WeChat:Sources:Material=false"),
        WeChatSources.Callback => options.Callback.IsActive
            ? (true, "已启用（/wechat/callback）")
            : (false, "未启用（默认关闭；需 WeChat:Callback:Enabled=true 且配置 Token）"),
        _ => (false, string.Empty)
    };
}
