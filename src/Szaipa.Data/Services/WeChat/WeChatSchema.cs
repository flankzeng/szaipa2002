using System.Data.Common;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// <c>WeChatSyncRecord</c> 是加性新表，在用户执行 <c>docs/sql/2026-10-26-wechat-sync.sql</c> 之前并不存在。
/// 所有读写路径都用这里的判定来降级（前台照旧显示全部 News；后台同步页提示先执行 SQL），绝不 500。
/// 模式与 Publication 的 ExhibitionWork 探测一致：按异常消息识别"表不存在"。
/// </summary>
public static class WeChatSchema
{
    public const string MissingTableMessage =
        "尚未建立 WeChatSyncRecord 表。请先在数据库执行 docs/sql/2026-10-26-wechat-sync.sql，再使用公众号同步。";

    public static bool IsMissingSyncTable(Exception exception)
    {
        // 生产 SQL Server 是中文环境，报错为「对象名 'WeChatSyncRecord' 无效。」，只按英文消息匹配会漏判、首页直接 500；
        // 统一交给 SchemaCompatibility：SQL Server 按错误号 208，SQLite/英文环境按消息。
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException db && SchemaCompatibility.IsMissingTable(db, "WeChatSyncRecord"))
            {
                return true;
            }
        }

        return false;
    }
}
