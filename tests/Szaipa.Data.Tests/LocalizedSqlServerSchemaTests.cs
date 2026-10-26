using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Szaipa.Data.Services;
using Szaipa.Data.Services.Home;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 生产 SQL Server 是中文环境：缺表时报「对象名 'X' 无效。」（错误号 208），缺列「列名 'X' 无效。」（207）。
/// 只按英文消息匹配的探测会漏判，导致执行增量 SQL 之前首页 500（2026-10-26 实测 WeChatSyncRecord 曾如此）。
/// SqlException 没有公开构造函数，这里用反射造一个与生产一致的中文异常。
/// </summary>
public sealed class LocalizedSqlServerSchemaTests
{
    [Fact]
    public void Chinese_invalid_object_name_is_recognized_by_every_missing_table_detector()
    {
        var missingSync = CreateSqlException(208, "对象名 'WeChatSyncRecord' 无效。");

        Assert.True(WeChatSchema.IsMissingSyncTable(missingSync));
        Assert.True(WeChatSchema.IsMissingSyncTable(new InvalidOperationException("wrapped", missingSync)));
        Assert.True(SchemaCompatibility.IsMissingTable(missingSync, "WeChatSyncRecord"));
        Assert.True(MissingSchema.IsMissingTable(missingSync, "WeChatSyncRecord"));
        Assert.True(HomeBlocksReadRepository.IsMissingSchemaObject(CreateSqlException(208, "对象名 'OrgMember' 无效。")));
    }

    [Fact]
    public void Chinese_invalid_column_name_is_recognized()
    {
        var missingColumn = CreateSqlException(207, "列名 'ThemeColor' 无效。");

        Assert.True(SchemaCompatibility.IsMissingColumn(missingColumn));
        Assert.True(MissingSchema.IsMissingColumn(missingColumn));
    }

    [Fact]
    public void Other_sql_errors_and_other_tables_are_not_swallowed()
    {
        Assert.False(WeChatSchema.IsMissingSyncTable(CreateSqlException(208, "对象名 'News' 无效。")));
        Assert.False(WeChatSchema.IsMissingSyncTable(CreateSqlException(18456, "用户 'szaipa_ro' 登录失败。")));
        Assert.False(SchemaCompatibility.IsMissingColumn(CreateSqlException(547, "INSERT 语句与 FOREIGN KEY 约束冲突。")));
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var errorCtor = typeof(SqlError).GetConstructors(flags)
            .OrderByDescending(c => c.GetParameters().Length)
            .First(c => c.GetParameters().FirstOrDefault()?.ParameterType == typeof(int));
        var args = errorCtor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => number,
            "errorMessage" => message,
            _ => p.ParameterType == typeof(string) ? string.Empty
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
                : null
        }).ToArray();
        var error = (SqlError)errorCtor.Invoke(args);

        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", flags)!.Invoke(collection, new object[] { error });

        var create = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.Name == "CreateException" && m.GetParameters() is var p
                && p.Length == 2 && p[0].ParameterType == typeof(SqlErrorCollection) && p[1].ParameterType == typeof(string));
        var exception = (SqlException)create.Invoke(null, new object[] { collection, "16.0.0" })!;
        Assert.Equal(number, exception.Number);
        Assert.IsAssignableFrom<DbException>(exception);
        return exception;
    }
}
