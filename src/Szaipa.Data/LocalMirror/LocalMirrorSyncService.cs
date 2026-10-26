using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Szaipa.Data.LocalMirror;

/// <summary>mirror-sync 拷贝参数。</summary>
public sealed class MirrorSyncSettings
{
    /// <summary>每批读/写的行数。</summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>
    /// 大日志表只拷最近 N 天（≤0 或 null 表示全量）。默认 90 天，避免一次 SELECT 拖垮生产。
    /// 需要过滤的表见 <see cref="DateFilteredTables"/>。
    /// </summary>
    public int? LogRetentionDays { get; init; } = 90;

    /// <summary>参与「最近 N 天」过滤的表 → 日期列。其余表一律全量。</summary>
    public IReadOnlyDictionary<string, string> DateFilteredTables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AccessData"] = "Date",
            ["Diary"] = "Date"
        };

    /// <summary>过滤的「今天」基准，便于单测固定时间。</summary>
    public DateTime Today { get; init; } = DateTime.Today;

    public MirrorDateFilter? FilterFor(string table)
    {
        if (LogRetentionDays is not > 0 || !DateFilteredTables.TryGetValue(table, out var column))
        {
            return null;
        }

        return new MirrorDateFilter(column, Today.AddDays(-LogRetentionDays.Value));
    }
}

/// <summary>单张表的拷贝结果。</summary>
public sealed record MirrorTableResult(
    string Table,
    long SourceRows,
    long CopiedRows,
    string Status,
    string? FilterDescription,
    IReadOnlyList<string> MissingSourceColumns,
    int NullsReplacedWithDefault,
    int DuplicateKeysSkipped = 0);

/// <summary>
/// 把来源库逐表、分批拷进目标 DbContext（SQLite）。按目标上下文的 EF 模型逐实体处理：
/// <list type="bullet">
/// <item>模型里新增的实体（T1–T4 的新表）若来源没有对应表 → 跳过，保留 EnsureCreated 建的空表；</item>
/// <item>模型里新增的列若来源没有 → 只拷两边共有的列，新列取 CLR 默认值 / 数据库默认值；</item>
/// <item>保留原 Id（实体带显式主键，SQLite 自增序列自动跟上）；</item>
/// <item>走 EF 实体写入而不是手拼 SQL，所以 DateTime/bool?/decimal 的 SQLite 存储格式与 EF 读取完全一致。</item>
/// </list>
/// 调用方负责建库（<c>EnsureCreated</c>）；本类不碰来源库之外的任何东西，来源只读。
/// </summary>
public sealed class LocalMirrorSyncService
{
    public async Task<IReadOnlyList<MirrorTableResult>> CopyAsync(
        IMirrorSource source,
        DbContext target,
        MirrorSyncSettings settings,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        target.ChangeTracker.AutoDetectChangesEnabled = false;
        var results = new List<MirrorTableResult>();

        foreach (var entityType in target.Model.GetEntityTypes().OrderBy(e => e.GetTableName(), StringComparer.Ordinal))
        {
            var result = await CopyTableAsync(source, target, entityType, settings, cancellationToken);
            results.Add(result);
            log(Format(result));
        }

        return results;
    }

    public static string Format(MirrorTableResult result)
    {
        var line = $"  {result.Table,-16} {result.CopiedRows,9} 行  [{result.Status}]";
        if (result.FilterDescription is not null)
        {
            line += $"  （{result.FilterDescription}，来源满足条件共 {result.SourceRows} 行）";
        }

        if (result.MissingSourceColumns.Count > 0)
        {
            line += $"  来源缺列(取默认值): {string.Join(",", result.MissingSourceColumns)}";
        }

        if (result.NullsReplacedWithDefault > 0)
        {
            line += $"  NULL→默认值 {result.NullsReplacedWithDefault} 处";
        }

        if (result.DuplicateKeysSkipped > 0)
        {
            line += $"  来源主键重复，仅保留首条、跳过 {result.DuplicateKeysSkipped} 行";
        }

        return line;
    }

    private static async Task<MirrorTableResult> CopyTableAsync(
        IMirrorSource source,
        DbContext target,
        IEntityType entityType,
        MirrorSyncSettings settings,
        CancellationToken cancellationToken)
    {
        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"Entity {entityType.DisplayName()} has no table mapping.");
        var key = entityType.FindPrimaryKey()?.Properties.SingleOrDefault()
            ?? throw new InvalidOperationException($"Entity {entityType.DisplayName()} needs a single-column key.");
        var keyColumn = key.GetColumnName();

        var sourceColumns = await source.GetColumnsAsync(table, cancellationToken);
        if (sourceColumns is null)
        {
            return new MirrorTableResult(table, 0, 0, "来源无此表，保留空表", null, Array.Empty<string>(), 0);
        }

        var sourceColumnSet = new HashSet<string>(sourceColumns, StringComparer.OrdinalIgnoreCase);
        var mapped = entityType.GetProperties()
            .Where(p => !p.IsShadowProperty() && p.PropertyInfo is not null)
            .ToList();
        var copyProps = mapped.Where(p => sourceColumnSet.Contains(p.GetColumnName())).ToList();
        var missing = mapped.Where(p => !sourceColumnSet.Contains(p.GetColumnName()))
            .Select(p => p.GetColumnName())
            .ToList();
        if (!copyProps.Contains(key))
        {
            throw new InvalidOperationException($"Source table {table} has no key column {keyColumn}.");
        }

        var filter = settings.FilterFor(table);
        if (filter is not null && !sourceColumnSet.Contains(filter.Column))
        {
            filter = null;
        }

        var sourceRows = await source.CountAsync(table, filter, cancellationToken);
        var columnNames = copyProps.Select(p => p.GetColumnName()).ToList();
        var keyIndex = copyProps.IndexOf(key);
        var copied = 0L;
        var nullFixups = 0;
        var duplicates = 0;
        var afterKey = long.MinValue;

        await using var transaction = await target.Database.BeginTransactionAsync(cancellationToken);
        while (true)
        {
            var rows = await source.ReadBatchAsync(
                table, columnNames, keyColumn, afterKey, settings.BatchSize, filter, cancellationToken);
            if (rows.Count == 0)
            {
                break;
            }

            long? previousKey = null;
            var batchAdded = 0;
            foreach (var row in rows)
            {
                // 生产库部分表没有真正的主键约束（如 TongouWorks 的 id=211 出现两次），而镜像库的主键唯一。
                // 结果按主键升序，重复键必相邻：只保留首条。
                var rowKey = Convert.ToInt64(row[keyIndex], CultureInfo.InvariantCulture);
                if (previousKey == rowKey)
                {
                    duplicates++;
                    continue;
                }

                previousKey = rowKey;
                var entity = Activator.CreateInstance(entityType.ClrType)
                    ?? throw new InvalidOperationException($"Cannot create {entityType.ClrType}.");
                for (var i = 0; i < copyProps.Count; i++)
                {
                    var property = copyProps[i];
                    var raw = row[i];
                    if (raw is null or DBNull)
                    {
                        if (!property.IsNullable)
                        {
                            nullFixups++;
                        }

                        continue;
                    }

                    property.PropertyInfo!.SetValue(entity, ConvertValue(raw, property.ClrType));
                }

                target.Add(entity);
                batchAdded++;
            }

            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();

            copied += batchAdded;
            afterKey = Convert.ToInt64(rows[^1][keyIndex], CultureInfo.InvariantCulture);
        }

        await transaction.CommitAsync(cancellationToken);

        var description = filter is null
            ? null
            : $"仅 {filter.Column} ≥ {filter.Since:yyyy-MM-dd}";
        return new MirrorTableResult(
            table, sourceRows, copied, "ok", description, missing, nullFixups, duplicates);
    }

    private static object ConvertValue(object raw, Type clrType)
    {
        var targetType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (targetType.IsInstanceOfType(raw))
        {
            return raw;
        }

        if (targetType == typeof(bool))
        {
            return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }
}
