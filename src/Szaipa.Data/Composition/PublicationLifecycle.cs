using System.Linq.Expressions;
using Szaipa.Data.Contexts.Szaipa;

namespace Szaipa.Data.Composition;

/// <summary>展览生命周期（由 <see cref="PublicationLifecycleCalculator"/> 按日期自动计算）。</summary>
public enum PublicationLifecycle
{
    /// <summary>即将开幕：开幕日期晚于今天。</summary>
    Upcoming = 0,

    /// <summary>展览中：今天落在 [开幕, 结束] 之内（缺日期按宽松处理，见计算器）。</summary>
    Ongoing = 1,

    /// <summary>已结束：结束日期早于今天，或后台手动标记为「已结束」。</summary>
    Ended = 2,

    /// <summary>下架：后台状态为「隐藏」；不出现在列表与首页，仅知道链接的人可直接访问详情。</summary>
    Offline = 3
}

/// <summary>
/// 展览状态自动流转的唯一入口（纯函数，前台列表 / 首页 / 后台列表徽标与筛选共用）。
///
/// <para><b>Legacy 里 <c>Publication.Status</c> 的真实用法</b>（newIndex.cshtml + StaffController）：它是一个
/// 手工三态开关，只影响首页「展会活动」——<c>true</c> 渲染在「近期活动」，<c>false</c> 渲染在「过往活动」，
/// <c>null</c> 两处都不渲染。旧后台没有展览录入模块，所以该开关只能靠改库；slug 迁移脚本也把全部历史展览写成
/// <c>Status = 0</c>（即「已结束」）。因此新语义必须保持：false = 已结束、null = 不展示，不能把 false 当成下架。</para>
///
/// <para><b>新语义</b>：<c>Status == true</c> 表示「自动」——按日期流转；<c>Status == false</c> 表示手动强制「已结束」
/// （不看日期，用于提前撤展 / 历史归档）；<c>Status == null</c> 表示「下架」。</para>
///
/// <para>自动规则（以天为粒度，忽略时间部分）：开幕日期 &gt; 今天 → 即将开幕；结束日期 &lt; 今天 → 已结束；
/// 其余 → 展览中。只填开幕日期的展览（如 92011，无结束日期）开幕后视为展览中，直到后台改为「已结束」；
/// 两个日期都空视为展览中。</para>
/// </summary>
public static class PublicationLifecycleCalculator
{
    public static PublicationLifecycle Compute(bool? status, DateTime? startDate, DateTime? endDate, DateTime today)
    {
        if (status is null)
        {
            return PublicationLifecycle.Offline;
        }

        if (status == false)
        {
            return PublicationLifecycle.Ended;
        }

        var day = today.Date;
        if (startDate is { } start && start.Date > day)
        {
            return PublicationLifecycle.Upcoming;
        }

        if (endDate is { } end && end.Date < day)
        {
            return PublicationLifecycle.Ended;
        }

        return PublicationLifecycle.Ongoing;
    }

    /// <summary>中文标签（徽标 / 分组标题共用）。</summary>
    public static string Label(PublicationLifecycle lifecycle) => lifecycle switch
    {
        PublicationLifecycle.Upcoming => "即将开幕",
        PublicationLifecycle.Ongoing => "展览中",
        PublicationLifecycle.Ended => "已结束",
        _ => "已下架"
    };

    /// <summary>
    /// 与 <see cref="Compute"/> 等价、可翻译为 SQL 的谓词，供后台列表按状态筛选。
    /// 日期比较用 <c>&gt;= 明天 0 点</c> / <c>&lt; 今天 0 点</c>，等价于忽略时间部分的 <c>Date</c> 比较。
    /// </summary>
    public static Expression<Func<Publication, bool>> Matches(PublicationLifecycle lifecycle, DateTime today)
    {
        var dayStart = today.Date;
        var tomorrow = dayStart.AddDays(1);
        return lifecycle switch
        {
            PublicationLifecycle.Offline => p => p.Status == null,
            PublicationLifecycle.Upcoming => p => p.Status == true && p.StartDate != null && p.StartDate >= tomorrow,
            PublicationLifecycle.Ended => p => p.Status == false
                || (p.Status == true
                    && !(p.StartDate != null && p.StartDate >= tomorrow)
                    && p.EndDate != null && p.EndDate < dayStart),
            _ => p => p.Status == true
                && !(p.StartDate != null && p.StartDate >= tomorrow)
                && !(p.EndDate != null && p.EndDate < dayStart)
        };
    }
}
