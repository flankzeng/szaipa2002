using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Services;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class SchemaCompatibilityTests
{
    [Fact]
    public void IsMissingColumn_recognizes_a_real_sqlite_insert_missing_column_exception_and_wrapper()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE Publication (Id INTEGER PRIMARY KEY, TitleCN TEXT NOT NULL);";
            create.ExecuteNonQuery();
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Publication (TitleCN, ThemeColor) VALUES ('test', '#112233');";
        var sqliteException = Assert.Throws<SqliteException>(() => insert.ExecuteNonQuery());

        Assert.Equal(1, sqliteException.SqliteErrorCode);
        Assert.Contains("table Publication has no column named ThemeColor", sqliteException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(SchemaCompatibility.IsMissingColumn(sqliteException));
        Assert.True(SchemaCompatibility.IsMissingColumn(new DbUpdateException("Save failed.", sqliteException)));
    }

    [Fact]
    public void IsMissingColumn_does_not_treat_other_sqlite_error_code_one_messages_as_missing_columns()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELEC 1;";
        var sqliteException = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());

        Assert.Equal(1, sqliteException.SqliteErrorCode);
        Assert.False(SchemaCompatibility.IsMissingColumn(sqliteException));
    }
}
