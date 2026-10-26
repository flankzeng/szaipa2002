using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Szaipa.Data.Services;

/// <summary>
/// Recognises the "column/table not there yet" failures raised by SQL Server (production) and SQLite (tests /
/// local mirror) so additive schema features degrade to the previous behaviour instead of returning 500.
/// Anything else (connection, permission, data errors) is deliberately NOT matched and keeps propagating.
///
/// SQL Server error text is localised (the production server is zh-CN: "对象名 'X' 无效。"), so SQL Server is
/// recognised by error NUMBER (207 invalid column name / 208 invalid object name); the English message check
/// remains for SQLite (tests / local mirror) and English servers.
/// </summary>
public static class SchemaCompatibility
{
    private const int SqlInvalidColumnName = 207;
    private const int SqlInvalidObjectName = 208;

    public static bool IsMissingColumn(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: SqlInvalidColumnName })
            {
                return true;
            }

            var message = current.Message;
            if (current is SqliteException sqlite)
            {
                if (sqlite.SqliteErrorCode == 1
                    && (message.Contains("no such column", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("has no column named", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                continue;
            }

            if (message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such column", StringComparison.OrdinalIgnoreCase)
                || message.Contains("has no column named", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMissingTable(DbException exception, string tableName)
    {
        if (exception is SqlException sql
            && sql.Number == SqlInvalidObjectName
            && sql.Message.Contains(tableName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains(tableName, StringComparison.OrdinalIgnoreCase)
            && (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such table", StringComparison.OrdinalIgnoreCase));
    }
}
