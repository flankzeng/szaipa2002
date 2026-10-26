namespace Szaipa.Data.LocalMirror;

/// <summary>
/// mirror-sync 的数据来源抽象（生产库的「只读」视图）。正式实现 <see cref="SqlServerMirrorSource"/> 只发 SELECT；
/// 抽象出来是为了单测可以用内存假数据驱动拷贝逻辑，不碰任何真实库。
/// </summary>
public interface IMirrorSource
{
    /// <summary>来源表的列名；表不存在返回 null（如 T1–T4 新增、生产尚未建的表）。</summary>
    Task<IReadOnlyList<string>?> GetColumnsAsync(string table, CancellationToken cancellationToken);

    /// <summary>满足过滤条件的行数（仅用于进度提示）。</summary>
    Task<long> CountAsync(string table, MirrorDateFilter? filter, CancellationToken cancellationToken);

    /// <summary>
    /// 按主键 keyset 分页读一批：<c>key &gt; afterKey</c>，按 key 升序取最多 <paramref name="take"/> 行。
    /// 每行是与 <paramref name="columns"/> 同序的值数组（NULL 为 null 或 DBNull）。
    /// </summary>
    Task<IReadOnlyList<object?[]>> ReadBatchAsync(
        string table,
        IReadOnlyList<string> columns,
        string keyColumn,
        long afterKey,
        int take,
        MirrorDateFilter? filter,
        CancellationToken cancellationToken);
}

/// <summary>只拷日期列 ≥ <see cref="Since"/> 的行（用于 AccessData、Diary 等大日志表）。</summary>
public sealed record MirrorDateFilter(string Column, DateTime Since);
