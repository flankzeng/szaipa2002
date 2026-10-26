using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.LocalMirror;

/// <summary>
/// 镜像模式的 DI 接线，集中在这里以便 <c>AddSzaipaData</c> 只多几行调用。
/// </summary>
internal static class LocalMirrorRegistration
{
    /// <summary>
    /// 镜像启用时，把三组选项改写成「指向 SQLite 镜像文件、视为已配置」。必须在原有 PostConfigure 之后注册
    /// （选项后配置按注册顺序执行），这样即使本机 Local.json 里还有 UseReadOnlyConnectionForDebug，
    /// 也不会再回落到生产只读连接。
    /// </summary>
    public static void ConfigureOptions(IServiceCollection services, LocalMirrorOptions mirror)
    {
        if (!mirror.Enabled)
        {
            return;
        }

        // 读侧：让连接策略、健康检查、HomeController 的「读模型是否激活」判断都看到「已配置、只读」。
        services.PostConfigure<LegacyDataOptions>(options =>
        {
            Apply(options.Szaipa, mirror.SzaipaConnectionString);
            Apply(options.Tongou, mirror.TongouConnectionString);
        });

        // 写侧：后台门控（登录页/仪表盘/运维页检查 IsConfigured）在镜像模式下视为已配置。
        services.PostConfigure<AdminWriteOptions>(options =>
        {
            options.EnableWrites = true;
            options.ConnectionString = mirror.SzaipaConnectionString;
            options.ConnectionStringSource = "LocalMirror";
            options.LocalMirrorActive = true;
            options.MirrorDebugLoginEnabled = mirror.DebugLoginAllowed;
        });

        services.PostConfigure<TongouAdminWriteOptions>(options =>
        {
            options.EnableWrites = true;
            options.ConnectionString = mirror.TongouConnectionString;
            options.ConnectionStringSource = "LocalMirror";
        });
    }

    /// <summary>
    /// 按是否镜像选择提供程序。镜像文件不存在时给出明确指引（SQLite 连接不存在的文件会静默新建空库，
    /// 之后每个查询都报 no such table，很难排查）。
    /// </summary>
    public static void UseProvider(DbContextOptionsBuilder options, bool mirrorEnabled, string connectionString)
    {
        if (!mirrorEnabled)
        {
            options.UseSqlServer(connectionString);
            return;
        }

        var path = LocalMirrorOptions.GetDataSource(connectionString);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"LocalMirror is enabled but the mirror database file does not exist: {path}. "
                + "Run `dotnet run --project src/Szaipa.Web -- mirror-sync` first to build it from the "
                + "read-only production connection.");
        }

        options.UseSqlite(connectionString);
    }

    private static void Apply(LegacyDatabaseOptions options, string connectionString)
    {
        options.AccessMode = LegacyAccessMode.ReadOnly;
        options.ConnectionString = connectionString;
        options.ConnectionStringSource = "LocalMirror";
    }
}
