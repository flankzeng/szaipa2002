using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Szaipa.Data.LocalMirror;

/// <summary>
/// 本地镜像库（LocalMirror）配置。开发机没有 Docker/SQL Server，所以把生产库「只读 SELECT」后落成两个 SQLite
/// 文件（szaipa.db / tongou.db），启用后四个上下文（Szaipa/Tongou × 读/写）全部改连这两个文件：
/// 读上下文仍禁 SaveChanges，只有 admin 写上下文可写。这样后台录入 → 前台展示可以在本机端到端验证，
/// 而生产库零写入。
/// <para>
/// 配置节 <c>LocalMirror</c>：<c>Enabled</c>（默认 false）、<c>SzaipaPath</c>、<c>TongouPath</c>
/// （默认仓库根 <c>.local-db/szaipa.db</c> / <c>.local-db/tongou.db</c>，相对路径按仓库根解析）、
/// <c>DebugLogin</c>（默认 true，仅 Development 生效，见 <see cref="DebugLoginAllowed"/>）。
/// </para>
/// <para>安全：<c>Production</c> 环境下启用必须启动失败，见 <see cref="EnsureAllowed"/>。</para>
/// </summary>
public sealed class LocalMirrorOptions
{
    public const string SectionName = "LocalMirror";

    /// <summary>仓库根的标志文件（解决方案文件），用于从 cwd / 程序目录向上定位仓库根。</summary>
    public const string RepositoryMarkerFile = "Szaipa.Modernization.slnx";

    public const string DefaultSzaipaFileName = "szaipa.db";

    public const string DefaultTongouFileName = "tongou.db";

    public const string DefaultDirectoryName = ".local-db";

    /// <summary>总开关。默认 false；启用后四个上下文都改用 SQLite 镜像文件。</summary>
    public bool Enabled { get; init; }

    /// <summary>Szaipa 镜像 SQLite 文件的绝对路径。</summary>
    public string SzaipaPath { get; init; } = string.Empty;

    /// <summary>Tongou 镜像 SQLite 文件的绝对路径。</summary>
    public string TongouPath { get; init; } = string.Empty;

    /// <summary>
    /// 镜像模式下是否开放固定调试账号登录（用户名/密码取 <c>AdminWrite:DebugUserName/DebugPassword</c>，默认 debug/debug）。
    /// 镜像里 Staff 表是生产的 MD5 密码，不要破解；本机用调试账号进后台。仅 Development 环境才会生效。
    /// </summary>
    public bool DebugLogin { get; init; } = true;

    /// <summary>解析出的宿主环境名；拿不到时按 ASP.NET Core 默认视为 Production（fail-closed）。</summary>
    public string EnvironmentName { get; init; } = "Production";

    public bool IsProduction =>
        string.Equals(EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);

    public bool IsDevelopment =>
        string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>仅「启用镜像 + Development + DebugLogin 未关」时开放镜像调试登录。</summary>
    public bool DebugLoginAllowed => Enabled && DebugLogin && IsDevelopment;

    public string SzaipaConnectionString => ToConnectionString(SzaipaPath);

    public string TongouConnectionString => ToConnectionString(TongouPath);

    public static string ToConnectionString(string path) => $"Data Source={path}";

    /// <summary>
    /// 启用镜像却处于 Production 环境时抛异常（启动失败）。镜像是开发机专用的 SQLite 副本，
    /// 生产站点绝不能误连它——否则会把线上读写都悄悄指向一个陈旧的本地文件。
    /// </summary>
    public void EnsureAllowed()
    {
        if (Enabled && IsProduction)
        {
            throw new InvalidOperationException(
                "LocalMirror:Enabled=true is not allowed in the Production environment. The local mirror is a "
                + "developer-machine SQLite copy; the production site must use the real database. Remove "
                + "LocalMirror:Enabled (or run with --environment Development).");
        }
    }

    public static LocalMirrorOptions FromConfiguration(IConfiguration configuration, string? repositoryRoot = null)
    {
        var section = configuration.GetSection(SectionName);
        var root = repositoryRoot ?? FindRepositoryRoot();
        var directory = Path.Combine(root, DefaultDirectoryName);

        return new LocalMirrorOptions
        {
            Enabled = section.GetValue<bool>("Enabled"),
            SzaipaPath = ResolvePath(section["SzaipaPath"], root, Path.Combine(directory, DefaultSzaipaFileName)),
            TongouPath = ResolvePath(section["TongouPath"], root, Path.Combine(directory, DefaultTongouFileName)),
            DebugLogin = section.GetValue("DebugLogin", true),
            EnvironmentName = ResolveEnvironmentName(configuration)
        };
    }

    /// <summary>
    /// 宿主环境名。优先取 Program.cs 写入配置的 <c>environment</c>（命令行 --environment 已裁决），
    /// 其次 ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT；都没有按 ASP.NET Core 默认视为 Production。
    /// </summary>
    public static string ResolveEnvironmentName(IConfiguration configuration)
    {
        foreach (var key in new[] { "environment", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT" })
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "Production";
    }

    /// <summary>从 cwd、再从程序目录向上找 <see cref="RepositoryMarkerFile"/>；找不到退回 cwd。</summary>
    public static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, RepositoryMarkerFile)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>从 SQLite 连接串取出文件路径。</summary>
    public static string GetDataSource(string connectionString) =>
        new SqliteConnectionStringBuilder(connectionString).DataSource;

    private static string ResolvePath(string? configured, string repositoryRoot, string fallback)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return fallback;
        }

        var trimmed = configured.Trim();
        return Path.IsPathRooted(trimmed) ? trimmed : Path.GetFullPath(Path.Combine(repositoryRoot, trimmed));
    }
}
