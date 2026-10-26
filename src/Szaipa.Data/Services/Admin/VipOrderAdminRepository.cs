using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Home;

namespace Szaipa.Data.Services.Admin;

/// <summary>后台「会员风采排序」列表的一行：艺术家基本信息 + 当前是否对前台显示。</summary>
public sealed record VipOrderItem(int ArtistId, string NameCn, string NameEn, string AvatarPath, bool IsHidden);

public interface IVipOrderAdminRepository
{
    /// <summary>全部艺术家，顺序与前台 Vip 页一致（含已隐藏者）。缺表时抛 DbException，由控制器给出「需先执行 SQL」提示。</summary>
    Task<IReadOnlyList<VipOrderItem>> GetItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 按给定的艺术家 Id 顺序保存：请求里出现的（忽略不存在/重复的）排在前面，没出现的保持现有顺序接在后面，
    /// 整体重排 1..n；隐藏状态不变。
    /// </summary>
    Task SaveOrderAsync(IReadOnlyList<int> orderedArtistIds, AdminActor actor, CancellationToken cancellationToken);

    /// <summary>显示/隐藏某位会员。艺术家不存在返回 false。</summary>
    Task<bool> SetHiddenAsync(int artistId, bool hidden, AdminActor actor, CancellationToken cancellationToken);
}

/// <summary>
/// 会员风采（Vip）排序与显隐的写仓储。每次写入都会把「当前前台顺序」固化成每位艺术家一行
/// <see cref="ArtistDisplay"/>（Sort=1..n），这样之后新增的会员（没有记录）统一排在最后，而已有顺序不会因为
/// 局部改动而漂移。不改 Artist 表。
/// </summary>
public sealed class VipOrderAdminRepository : IVipOrderAdminRepository
{
    private readonly SzaipaAdminContext _db;
    private readonly IOperationRecorder _operationRecorder;

    public VipOrderAdminRepository(SzaipaAdminContext db, IOperationRecorder operationRecorder)
    {
        _db = db;
        _operationRecorder = operationRecorder;
    }

    public async Task<IReadOnlyList<VipOrderItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        var (items, displays) = await LoadAsync(tracking: false, cancellationToken);
        return Arrange(items, displays);
    }

    public async Task SaveOrderAsync(
        IReadOnlyList<int> orderedArtistIds,
        AdminActor actor,
        CancellationToken cancellationToken)
    {
        var (items, displays) = await LoadAsync(tracking: true, cancellationToken);
        var current = Arrange(items, displays);
        var byId = current.ToDictionary(item => item.ArtistId);

        var ordered = new List<int>(current.Count);
        var seen = new HashSet<int>();
        foreach (var id in orderedArtistIds)
        {
            if (byId.ContainsKey(id) && seen.Add(id))
            {
                ordered.Add(id);
            }
        }

        ordered.AddRange(current.Where(item => !seen.Contains(item.ArtistId)).Select(item => item.ArtistId));

        Materialize(ordered, displays);
        await _operationRecorder.RecordAsync(_db, actor, $"调整了会员风采（Vip）顺序，共 {ordered.Count} 位。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SetHiddenAsync(int artistId, bool hidden, AdminActor actor, CancellationToken cancellationToken)
    {
        var (items, displays) = await LoadAsync(tracking: true, cancellationToken);
        var target = items.FirstOrDefault(item => item.ArtistId == artistId);
        if (target is null)
        {
            return false;
        }

        var hasRecord = displays.ContainsKey(artistId);
        if (!hasRecord && !hidden)
        {
            return true; // 本来就显示，且没有记录，无需写入。
        }

        var order = Arrange(items, displays).Select(item => item.ArtistId).ToList();
        Materialize(order, displays);
        displays[artistId].IsHidden = hidden;

        var name = string.IsNullOrWhiteSpace(target.NameCn) ? $"#{artistId}" : target.NameCn;
        await _operationRecorder.RecordAsync(
            _db, actor, hidden ? $"在会员风采（Vip）中隐藏了「{name}」。" : $"在会员风采（Vip）中恢复显示「{name}」。", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(List<VipOrderItem> Items, Dictionary<int, ArtistDisplay> Displays)> LoadAsync(
        bool tracking,
        CancellationToken cancellationToken)
    {
        var items = await _db.Artist.AsNoTracking()
            .OrderByDescending(artist => artist.Id)
            .Select(artist => new VipOrderItem(
                artist.Id,
                artist.ArtistNameCN ?? string.Empty,
                artist.ArtistNameEN ?? string.Empty,
                artist.Path ?? string.Empty,
                false))
            .ToListAsync(cancellationToken);

        var query = tracking ? _db.ArtistDisplay : _db.ArtistDisplay.AsNoTracking();
        var displays = (await query.ToListAsync(cancellationToken)).ToDictionary(display => display.ArtistId);
        return (items, displays);
    }

    private static IReadOnlyList<VipOrderItem> Arrange(
        IReadOnlyList<VipOrderItem> items,
        IReadOnlyDictionary<int, ArtistDisplay> displays)
    {
        return VipDisplayOrder.Arrange(items, item => item.ArtistId, displays.Values, includeHidden: true)
            .Select(item => displays.TryGetValue(item.ArtistId, out var display) && display.IsHidden
                ? item with { IsHidden = true }
                : item)
            .ToList();
    }

    /// <summary>按给定顺序给每位艺术家写 Sort=1..n，缺记录的新建（未隐藏）。</summary>
    private void Materialize(IReadOnlyList<int> orderedArtistIds, Dictionary<int, ArtistDisplay> displays)
    {
        for (var index = 0; index < orderedArtistIds.Count; index++)
        {
            var id = orderedArtistIds[index];
            if (!displays.TryGetValue(id, out var display))
            {
                display = new ArtistDisplay { ArtistId = id };
                displays[id] = display;
                _db.ArtistDisplay.Add(display);
            }

            display.Sort = index + 1;
        }
    }
}
