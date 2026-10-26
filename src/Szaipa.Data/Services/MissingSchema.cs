using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Szaipa.Data.Services;

/// <summary>
/// 识别「老库还没执行增量 SQL」造成的缺表/缺列异常（SQL Server：Invalid object/column name；SQLite：no such
/// table/column）。读侧对 T4 新增的表/列用它做能力探测降级，保证前台绝不因为 schema 落后而 500。
/// </summary>
internal static class MissingSchema
{
    public static bool IsMissingTable(DbException exception, string table) =>
        (exception is SqlException { Number: 208 }
         || exception is SqliteException { SqliteErrorCode: 1 }
            && exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
         || exception.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        && exception.Message.Contains(table, StringComparison.OrdinalIgnoreCase);

    public static bool IsMissingColumn(DbException exception) =>
        exception is SqlException { Number: 207 }
        || exception.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase);
}
