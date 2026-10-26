using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Services.Home;

/// <summary>
/// 会员风采（/Home/Vip）的排序与显隐规则，前台读仓储与后台排序页共用，保证「后台看到的顺序 = 前台顺序」。
/// 规则：有 <see cref="ArtistDisplay"/> 记录的按 Sort 升序（同 Sort 再按 Id 倒序）排在前面；没有记录的保持传入的
/// 原顺序（Vip 原本是 Id 倒序）排在后面；<c>IsHidden</c> 的在前台剔除（后台列表保留，以便取消隐藏）。
/// </summary>
public static class VipDisplayOrder
{
    public static List<T> Arrange<T>(
        IReadOnlyList<T> newestFirst,
        Func<T, int> idOf,
        IEnumerable<ArtistDisplay> displays,
        bool includeHidden)
    {
        var byArtist = new Dictionary<int, ArtistDisplay>();
        foreach (var display in displays)
        {
            byArtist[display.ArtistId] = display;
        }

        var recorded = new List<T>();
        var unrecorded = new List<T>();
        foreach (var item in newestFirst)
        {
            if (byArtist.TryGetValue(idOf(item), out var display))
            {
                if (includeHidden || !display.IsHidden)
                {
                    recorded.Add(item);
                }
            }
            else
            {
                unrecorded.Add(item);
            }
        }

        // OrderBy 是稳定排序：同 Sort 时保持 newestFirst 传入的相对顺序（即 Id 倒序）。
        var ordered = recorded.OrderBy(item => byArtist[idOf(item)].Sort).ToList();
        ordered.AddRange(unrecorded);
        return ordered;
    }
}
