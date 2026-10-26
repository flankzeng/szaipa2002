using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.TongouAdmin;
using Szaipa.Data.DependencyInjection;

namespace Szaipa.Data.LocalMirror;

/// <summary>
/// <c>mirror-sync</c> 命令的实现：用现有只读连接（<c>LegacyData:Szaipa/Tongou:ConnectionString</c>，db_datareader）
/// 对生产库做只读 SELECT，把数据重建成本地 SQLite 镜像文件。入口：
/// <c>dotnet run --project src/Szaipa.Web -- mirror-sync [--days N] [--only szaipa|tongou] [--batch N]</c>。
/// <list type="bullet">
/// <item>先写到 <c>&lt;目标&gt;.syncing</c> 临时文件，全部成功后才替换正式文件——中途断网/失败不会毁掉旧镜像；</item>
/// <item>删除并重建 = 每次都是干净全量，可重复执行；表结构由 <c>EnsureCreated()</c> 按 admin 上下文模型建，
/// 所以后续任务新增的实体会自动出现（来源没有的表为空表）；</item>
/// <item>不会打印连接串或任何凭据。</item>
/// </list>
/// </summary>
public static class LocalMirrorSyncRunner
{
    private const string UsageText =
        "用法: mirror-sync [--days N] [--only szaipa|tongou] [--batch N]\n"
        + "  --days N     AccessData/Diary 等大日志表只拷最近 N 天（默认 90；0 = 全量）\n"
        + "  --only X     只同步 szaipa 或 tongou（默认两个都同步）\n"
        + "  --batch N    每批行数（默认 500）";

    public static async Task<int> RunAsync(
        IConfiguration configuration,
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseArgs(args, out var days, out var only, out var batch, out var error))
        {
            await output.WriteLineAsync(error);
            await output.WriteLineAsync(UsageText);
            return 2;
        }

        var mirror = LocalMirrorOptions.FromConfiguration(configuration);
        var settings = new MirrorSyncSettings
        {
            LogRetentionDays = days,
            BatchSize = batch
        };

        var szaipaConnection = SzaipaDataServiceCollectionExtensions.ResolveReadOnlyConnection(
            configuration,
            connectionStringsKey: "ConnectionStrings:Szaipa",
            legacyDataKey: "LegacyData:Szaipa:ConnectionString",
            environmentKey: "SZAIPA_READONLY_CONNECTION");
        var tongouConnection = SzaipaDataServiceCollectionExtensions.ResolveReadOnlyConnection(
            configuration,
            connectionStringsKey: "ConnectionStrings:Tongou",
            legacyDataKey: "LegacyData:Tongou:ConnectionString",
            environmentKey: "TONGOU_READONLY_CONNECTION");

        var jobs = new List<(string Name, string? SourceConnection, string TargetPath, Func<DbContextOptions, DbContext> CreateTarget)>();
        if (only is null or "szaipa")
        {
            jobs.Add(("Szaipa", szaipaConnection, mirror.SzaipaPath, options => new SzaipaAdminContext(
                (DbContextOptions<SzaipaAdminContext>)options)));
        }

        if (only is null or "tongou")
        {
            jobs.Add(("Tongou", tongouConnection, mirror.TongouPath, options => new TongouAdminContext(
                (DbContextOptions<TongouAdminContext>)options)));
        }

        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.SourceConnection))
            {
                await output.WriteLineAsync(
                    $"[{job.Name}] 没有找到只读连接串。请在 appsettings.Local.json 配置 LegacyData:{job.Name}:ConnectionString"
                    + "（只读账号），或设置对应环境变量。");
                return 2;
            }
        }

        await output.WriteLineAsync(
            "mirror-sync: 对生产库只发 SELECT（只读账号）；结果写入本地 SQLite。"
            + (settings.LogRetentionDays is > 0
                ? $" 大日志表只拷最近 {settings.LogRetentionDays} 天。"
                : " 大日志表全量拷贝。"));

        foreach (var job in jobs)
        {
            await output.WriteLineAsync($"[{job.Name}] -> {job.TargetPath}");
            var code = await SyncOneAsync(job, settings, output, cancellationToken);
            if (code != 0)
            {
                return code;
            }
        }

        await output.WriteLineAsync("mirror-sync 完成。启动站点时设置 LocalMirror:Enabled=true 即可使用镜像库。");
        return 0;
    }

    private static async Task<int> SyncOneAsync(
        (string Name, string? SourceConnection, string TargetPath, Func<DbContextOptions, DbContext> CreateTarget) job,
        MirrorSyncSettings settings,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var finalPath = job.TargetPath;
        var tempPath = finalPath + ".syncing";

        try
        {
            EnsureSafeToReplace(finalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(finalPath))!);
            DeleteSqliteFiles(tempPath);

            IReadOnlyList<MirrorTableResult> results;
            await using (var source = new SqlServerMirrorSource(job.SourceConnection!))
            {
                results = await BuildMirrorAsync(job, source, tempPath, settings, output, cancellationToken);
            }

            // 全部成功后才替换旧镜像。
            SqliteConnection.ClearAllPools();
            DeleteSqliteFiles(finalPath);
            File.Move(tempPath, finalPath);

            await output.WriteLineAsync(
                $"[{job.Name}] 合计 {results.Sum(r => r.CopiedRows)} 行，{results.Count} 张表。");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SqliteConnection.ClearAllPools();
            DeleteSqliteFiles(tempPath);
            await output.WriteLineAsync(
                $"[{job.Name}] 同步失败，旧镜像未改动: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>在临时路径建库 + 拷数据，返回各表结果。上下文用完即释放（Pooling=False，文件立刻可移动）。</summary>
    private static async Task<IReadOnlyList<MirrorTableResult>> BuildMirrorAsync(
        (string Name, string? SourceConnection, string TargetPath, Func<DbContextOptions, DbContext> CreateTarget) job,
        IMirrorSource source,
        string tempPath,
        MirrorSyncSettings settings,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = tempPath,
            Pooling = false
        }.ToString();

        DbContextOptions options = job.Name == "Szaipa"
            ? new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connectionString).Options
            : new DbContextOptionsBuilder<TongouAdminContext>().UseSqlite(connectionString).Options;

        IReadOnlyList<MirrorTableResult> results;
        await using (var target = job.CreateTarget(options))
        {
            await target.Database.EnsureCreatedAsync(cancellationToken);
            // 批量灌数据时放宽同步，换取速度；这是一次性的临时文件，失败直接丢弃。
            await target.Database.ExecuteSqlRawAsync("PRAGMA synchronous = OFF", cancellationToken);

            var service = new LocalMirrorSyncService();
            results = await service.CopyAsync(source, target, settings, line => output.WriteLine(line), cancellationToken);

            // WAL：服务运行时读上下文与写上下文各持连接，WAL 下读写互不阻塞。该模式写进文件头，永久有效。
            await target.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL", cancellationToken);
        }

        return results;
    }

    /// <summary>
    /// 只允许覆盖「不存在」或「确实是 SQLite 文件」的目标，防止配置写错路径时误删别的文件。
    /// </summary>
    public static void EnsureSafeToReplace(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var header = new byte[16];
        using var stream = File.OpenRead(path);
        var read = stream.Read(header, 0, header.Length);
        if (read < 16 || !Encoding.ASCII.GetString(header).StartsWith("SQLite format 3", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to replace {path}: it exists but is not a SQLite database file. "
                + "Check LocalMirror:SzaipaPath / LocalMirror:TongouPath.");
        }
    }

    internal static void DeleteSqliteFiles(string path)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            var file = path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public static bool TryParseArgs(
        string[] args,
        out int? days,
        out string? only,
        out int batch,
        out string error)
    {
        days = 90;
        only = null;
        batch = 500;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--days" when i + 1 < args.Length
                    && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var d):
                    days = d;
                    i++;
                    break;
                case "--batch" when i + 1 < args.Length
                    && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var b)
                    && b > 0:
                    batch = b;
                    i++;
                    break;
                case "--only" when i + 1 < args.Length
                    && args[i + 1].ToLowerInvariant() is "szaipa" or "tongou":
                    only = args[i + 1].ToLowerInvariant();
                    i++;
                    break;
                default:
                    error = $"无法识别的参数: {args[i]}";
                    return false;
            }
        }

        return true;
    }
}
