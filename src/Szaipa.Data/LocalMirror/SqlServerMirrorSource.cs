using System.Text;
using Microsoft.Data.SqlClient;

namespace Szaipa.Data.LocalMirror;

/// <summary>
/// 用现有只读连接（<c>LegacyData:*:ConnectionString</c>，db_datareader 账号）对生产库做 SELECT。
/// <b>本类是 mirror-sync 触碰生产库的唯一入口，只会发出 <see cref="MirrorSql"/> 里的三种 SELECT 语句</b>
/// （读列名 / 计数 / keyset 分页读）；任何 INSERT/UPDATE/DELETE/DDL 都不存在于这条路径。
/// </summary>
public sealed class SqlServerMirrorSource : IMirrorSource, IAsyncDisposable
{
    private readonly SqlConnection _connection;

    public SqlServerMirrorSource(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public async Task<IReadOnlyList<string>?> GetColumnsAsync(string table, CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);
        await using var command = _connection.CreateCommand();
        command.CommandText = MirrorSql.SelectColumns;
        command.CommandTimeout = MirrorSql.CommandTimeoutSeconds;
        command.Parameters.AddWithValue("@table", table);

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns.Count == 0 ? null : columns;
    }

    public async Task<long> CountAsync(string table, MirrorDateFilter? filter, CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);
        await using var command = _connection.CreateCommand();
        command.CommandText = MirrorSql.BuildCount(table, filter);
        command.CommandTimeout = MirrorSql.CommandTimeoutSeconds;
        if (filter is not null)
        {
            command.Parameters.AddWithValue("@since", filter.Since);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<object?[]>> ReadBatchAsync(
        string table,
        IReadOnlyList<string> columns,
        string keyColumn,
        long afterKey,
        int take,
        MirrorDateFilter? filter,
        CancellationToken cancellationToken)
    {
        await EnsureOpenAsync(cancellationToken);
        await using var command = _connection.CreateCommand();
        command.CommandText = MirrorSql.BuildBatch(table, columns, keyColumn, filter);
        command.CommandTimeout = MirrorSql.CommandTimeoutSeconds;
        command.Parameters.AddWithValue("@take", take);
        command.Parameters.AddWithValue("@after", afterKey);
        if (filter is not null)
        {
            command.Parameters.AddWithValue("@since", filter.Since);
        }

        var rows = new List<object?[]>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new object?[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                values[i] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            }

            rows.Add(values);
        }

        return rows;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private async Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }
}

/// <summary>
/// mirror-sync 对生产库发出的全部 SQL 文本都在这里生成，且只有 SELECT。单测会断言每条语句以 SELECT 开头、
/// 不含任何写/DDL 关键字，作为「生产库只读」的回归护栏。
/// </summary>
public static class MirrorSql
{
    public const int CommandTimeoutSeconds = 120;

    public const string SelectColumns =
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table ORDER BY ORDINAL_POSITION";

    public static string BuildCount(string table, MirrorDateFilter? filter)
    {
        var sql = new StringBuilder("SELECT COUNT_BIG(*) FROM ").Append(Quote(table)).Append(" WITH (NOLOCK)");
        if (filter is not null)
        {
            sql.Append(" WHERE ").Append(Quote(filter.Column)).Append(" >= @since");
        }

        return sql.ToString();
    }

    public static string BuildBatch(
        string table,
        IReadOnlyList<string> columns,
        string keyColumn,
        MirrorDateFilter? filter)
    {
        var sql = new StringBuilder("SELECT TOP (@take) ")
            .Append(string.Join(", ", columns.Select(Quote)))
            .Append(" FROM ").Append(Quote(table)).Append(" WITH (NOLOCK) WHERE ")
            .Append(Quote(keyColumn)).Append(" > @after");
        if (filter is not null)
        {
            sql.Append(" AND ").Append(Quote(filter.Column)).Append(" >= @since");
        }

        sql.Append(" ORDER BY ").Append(Quote(keyColumn));
        return sql.ToString();
    }

    /// <summary>方括号引用标识符（表名/列名来自 EF 模型，不是用户输入，这里仍做转义兜底）。</summary>
    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
