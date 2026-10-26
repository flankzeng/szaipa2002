using System.Data.Common;

namespace Szaipa.Data.Services;

/// <summary>
/// Recognises the "column/table not there yet" failures raised by SQL Server (production) and SQLite (tests /
/// local mirror) so additive schema features degrade to the previous behaviour instead of returning 500.
/// Anything else (connection, permission, data errors) is deliberately NOT matched and keeps propagating.
/// </summary>
internal static class SchemaCompatibility
{
    public static bool IsMissingColumn(DbException exception)
    {
        var message = exception.Message;
        return message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no such column", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMissingTable(DbException exception, string tableName)
    {
        var message = exception.Message;
        return message.Contains(tableName, StringComparison.OrdinalIgnoreCase)
            && (message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                || message.Contains("no such table", StringComparison.OrdinalIgnoreCase));
    }
}
