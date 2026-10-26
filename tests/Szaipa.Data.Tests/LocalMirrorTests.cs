using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;
using Szaipa.Data.DependencyInjection;
using Szaipa.Data.LocalMirror;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 本地镜像库（LocalMirror）：选项解析、Production 拒绝、四个上下文的 provider 切换、读上下文仍禁写、
/// 后台写入前台可见，以及 mirror-sync 拷贝逻辑（用内存假来源，不连任何真实库）。
/// </summary>
public sealed class LocalMirrorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "szaipa-mirror-tests-" + Guid.NewGuid().ToString("N"));

    public LocalMirrorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    // ---------- 选项解析 ----------

    [Fact]
    public void Defaults_To_LocalDb_Under_Repository_Root_And_Is_Disabled()
    {
        var options = LocalMirrorOptions.FromConfiguration(Config(), repositoryRoot: _directory);

        Assert.False(options.Enabled);
        Assert.Equal(Path.Combine(_directory, ".local-db", "szaipa.db"), options.SzaipaPath);
        Assert.Equal(Path.Combine(_directory, ".local-db", "tongou.db"), options.TongouPath);
    }

    [Fact]
    public void Relative_Paths_Resolve_Against_Repository_Root_And_Absolute_Paths_Are_Kept()
    {
        var absolute = Path.Combine(_directory, "elsewhere", "t.db");
        var options = LocalMirrorOptions.FromConfiguration(
            Config(("LocalMirror:SzaipaPath", "data/s.db"), ("LocalMirror:TongouPath", absolute)),
            repositoryRoot: _directory);

        Assert.Equal(Path.Combine(_directory, "data", "s.db"), options.SzaipaPath);
        Assert.Equal(absolute, options.TongouPath);
    }

    [Fact]
    public void Environment_Falls_Back_To_Production_When_Unknown()
    {
        Assert.Equal("Production", LocalMirrorOptions.ResolveEnvironmentName(Config()));
        Assert.Equal("Development", LocalMirrorOptions.ResolveEnvironmentName(Config(("environment", "Development"))));
        Assert.Equal("Staging", LocalMirrorOptions.ResolveEnvironmentName(Config(("ASPNETCORE_ENVIRONMENT", "Staging"))));
    }

    [Theory]
    [InlineData("Production", true, true)]
    [InlineData("production", true, true)]
    [InlineData(null, true, true)]
    [InlineData("Development", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("Production", false, false)]
    public void Production_Rejects_Enabled_Mirror(string? environment, bool enabled, bool shouldThrow)
    {
        var settings = new List<(string, string?)> { ("LocalMirror:Enabled", enabled.ToString()) };
        if (environment is not null)
        {
            settings.Add(("environment", environment));
        }

        var options = LocalMirrorOptions.FromConfiguration(Config(settings.ToArray()), _directory);

        if (shouldThrow)
        {
            Assert.Throws<InvalidOperationException>(options.EnsureAllowed);
        }
        else
        {
            options.EnsureAllowed();
        }
    }

    [Fact]
    public void AddSzaipaData_Fails_Startup_When_Mirror_Enabled_In_Production()
    {
        var configuration = Config(("LocalMirror:Enabled", "true"), ("environment", "Production"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddSzaipaData(configuration));

        Assert.Contains("Production", exception.Message);
    }

    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Staging", true, false)]
    public void Mirror_Debug_Login_Is_Development_Only(string environment, bool debugLogin, bool expected)
    {
        var options = LocalMirrorOptions.FromConfiguration(
            Config(
                ("LocalMirror:Enabled", "true"),
                ("LocalMirror:DebugLogin", debugLogin.ToString()),
                ("environment", environment)),
            _directory);

        Assert.Equal(expected, options.DebugLoginAllowed);
    }

    // ---------- provider 切换 ----------

    [Fact]
    public void Mirror_Mode_Switches_All_Four_Contexts_To_Sqlite()
    {
        using var provider = BuildMirrorProvider(out _);
        using var scope = provider.CreateScope();

        Assert.True(IsSqlite(scope.ServiceProvider.GetRequiredService<SzaipaLegacyReadContext>()));
        Assert.True(IsSqlite(scope.ServiceProvider.GetRequiredService<TongouLegacyReadContext>()));
        Assert.True(IsSqlite(scope.ServiceProvider.GetRequiredService<SzaipaAdminContext>()));
        Assert.True(IsSqlite(scope.ServiceProvider.GetRequiredService<TongouAdminContext>()));
    }

    [Fact]
    public void Default_Mode_Keeps_Sql_Server_Provider_For_All_Four_Contexts()
    {
        const string fakeSqlServer = "Server=unreachable.invalid;Database=X;User Id=u;Password=p;";
        var configuration = Config(
            ("environment", "Development"),
            ("LegacyData:Szaipa:AccessMode", "ReadOnly"),
            ("LegacyData:Szaipa:ConnectionString", fakeSqlServer),
            ("LegacyData:Tongou:AccessMode", "ReadOnly"),
            ("LegacyData:Tongou:ConnectionString", fakeSqlServer),
            ("AdminWrite:EnableWrites", "true"),
            ("ConnectionStrings:SzaipaAdmin", fakeSqlServer),
            ("TongouAdminWrite:EnableWrites", "true"),
            ("ConnectionStrings:TongouAdmin", fakeSqlServer));
        var services = new ServiceCollection();
        services.AddSzaipaData(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // 只解析上下文、读 ProviderName，不会真的去连接。
        Assert.Contains("SqlServer", scope.ServiceProvider.GetRequiredService<SzaipaLegacyReadContext>().Database.ProviderName);
        Assert.Contains("SqlServer", scope.ServiceProvider.GetRequiredService<TongouLegacyReadContext>().Database.ProviderName);
        Assert.Contains("SqlServer", scope.ServiceProvider.GetRequiredService<SzaipaAdminContext>().Database.ProviderName);
        Assert.Contains("SqlServer", scope.ServiceProvider.GetRequiredService<TongouAdminContext>().Database.ProviderName);
    }

    [Fact]
    public void Mirror_Mode_Overrides_Readonly_Debug_Fallback_To_Production_Connection()
    {
        // 本机 Local.json 常见组合：UseReadOnlyConnectionForDebug=true + 生产只读连接串。镜像启用后绝不能回落到它。
        using var provider = BuildMirrorProvider(
            out var mirror,
            ("AdminWrite:EnableWrites", "true"),
            ("AdminWrite:UseReadOnlyConnectionForDebug", "true"),
            ("TongouAdminWrite:EnableWrites", "true"),
            ("TongouAdminWrite:UseReadOnlyConnectionForDebug", "true"),
            ("LegacyData:Szaipa:ConnectionString", "Server=prod.invalid;Database=Szaipa;User Id=ro;Password=secret;"),
            ("LegacyData:Tongou:ConnectionString", "Server=prod.invalid;Database=Tongou;User Id=ro;Password=secret;"));

        var admin = provider.GetRequiredService<IOptions<AdminWriteOptions>>().Value;
        var tongouAdmin = provider.GetRequiredService<IOptions<TongouAdminWriteOptions>>().Value;
        var legacy = provider.GetRequiredService<IOptions<LegacyDataOptions>>().Value;

        Assert.Equal(mirror.SzaipaConnectionString, admin.ConnectionString);
        Assert.Equal(mirror.TongouConnectionString, tongouAdmin.ConnectionString);
        Assert.Equal(mirror.SzaipaConnectionString, legacy.Szaipa.ConnectionString);
        Assert.Equal(mirror.TongouConnectionString, legacy.Tongou.ConnectionString);
        Assert.DoesNotContain("prod.invalid", admin.ConnectionString + tongouAdmin.ConnectionString);
    }

    [Fact]
    public void Mirror_Mode_Treats_Admin_Gating_As_Configured_Without_Any_Other_Settings()
    {
        using var provider = BuildMirrorProvider(out _);

        var admin = provider.GetRequiredService<IOptions<AdminWriteOptions>>().Value;
        var tongouAdmin = provider.GetRequiredService<IOptions<TongouAdminWriteOptions>>().Value;
        var policy = provider.GetRequiredService<ILegacyConnectionPolicy>();

        Assert.True(admin.IsConfigured);
        Assert.True(admin.LocalMirrorActive);
        Assert.Equal("LocalMirror", admin.ConnectionStringSource);
        Assert.True(admin.DebugLoginEnabled); // Development 默认开放镜像调试登录
        Assert.True(tongouAdmin.IsConfigured);

        var sources = policy.GetConfiguredSources();
        Assert.Equal(LegacyAccessMode.ReadOnly, sources[LegacyDataSource.Szaipa].AccessMode);
        Assert.Equal(LegacyAccessMode.ReadOnly, sources[LegacyDataSource.Tongou].AccessMode);
        Assert.Equal("LocalMirror", policy.GetSourceDiagnostics()[LegacyDataSource.Szaipa].ConnectionStringSource);
    }

    [Fact]
    public void Mirror_Debug_Login_Can_Be_Turned_Off()
    {
        using var provider = BuildMirrorProvider(out _, ("LocalMirror:DebugLogin", "false"));

        Assert.False(provider.GetRequiredService<IOptions<AdminWriteOptions>>().Value.DebugLoginEnabled);
    }

    [Fact]
    public void Default_Mode_Is_Untouched_By_Mirror_Wiring()
    {
        var services = new ServiceCollection();
        services.AddSzaipaData(Config(("environment", "Production")));
        using var provider = services.BuildServiceProvider();

        var admin = provider.GetRequiredService<IOptions<AdminWriteOptions>>().Value;
        Assert.False(provider.GetRequiredService<LocalMirrorOptions>().Enabled);
        Assert.False(admin.IsConfigured);
        Assert.False(admin.LocalMirrorActive);
        Assert.False(admin.DebugLoginEnabled);
    }

    [Fact]
    public void Missing_Mirror_File_Gives_Actionable_Error()
    {
        var services = new ServiceCollection();
        services.AddSzaipaData(Config(
            ("environment", "Development"),
            ("LocalMirror:Enabled", "true"),
            ("LocalMirror:SzaipaPath", Path.Combine(_directory, "missing.db")),
            ("LocalMirror:TongouPath", Path.Combine(_directory, "missing2.db"))));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<SzaipaLegacyReadContext>());

        Assert.Contains("mirror-sync", exception.Message);
        Assert.False(File.Exists(Path.Combine(_directory, "missing.db")));
    }

    // ---------- 读禁写 / 写可见 ----------

    [Fact]
    public async Task Mirror_Read_Contexts_Still_Block_SaveChanges()
    {
        using var provider = BuildMirrorProvider(out _);
        using var scope = provider.CreateScope();
        var szaipaRead = scope.ServiceProvider.GetRequiredService<SzaipaLegacyReadContext>();
        var tongouRead = scope.ServiceProvider.GetRequiredService<TongouLegacyReadContext>();

        Assert.Throws<InvalidOperationException>(() => szaipaRead.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => szaipaRead.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => tongouRead.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tongouRead.SaveChangesAsync());
    }

    [Fact]
    public async Task Mirror_Admin_Write_Is_Visible_Through_Read_Context()
    {
        using var provider = BuildMirrorProvider(out _);
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<SzaipaAdminContext>();
        var read = scope.ServiceProvider.GetRequiredService<SzaipaLegacyReadContext>();

        admin.News.Add(new News { Title = "镜像新闻", Date = new DateTime(2026, 10, 26, 9, 30, 0), original = true });
        await admin.SaveChangesAsync();

        var seen = await read.News.AsNoTracking().SingleAsync(n => n.Title == "镜像新闻");
        Assert.Equal(new DateTime(2026, 10, 26, 9, 30, 0), seen.Date);

        var tongouAdmin = scope.ServiceProvider.GetRequiredService<TongouAdminContext>();
        var tongouRead = scope.ServiceProvider.GetRequiredService<TongouLegacyReadContext>();
        tongouAdmin.TongouAtrist.Add(new TongouAtrist { Name = "镜像艺术家" });
        await tongouAdmin.SaveChangesAsync();
        Assert.Equal("镜像艺术家", (await tongouRead.TongouAtrist.AsNoTracking().SingleAsync()).Name);
    }

    // ---------- mirror-sync 拷贝逻辑 ----------

    [Fact]
    public async Task Sync_Copies_Rows_Preserving_Ids_And_Value_Types()
    {
        await using var fixture = OpenSzaipaTarget();
        var source = new FakeMirrorSource()
            .Table("News", new[] { "Id", "Title", "original", "Important", "Date", "ReadCount", "Year" },
                Row(5, "第五条", true, null, new DateTime(2026, 1, 2, 3, 4, 5), 7, 2026),
                Row(9, "第九条", false, true, null, 0, null));

        var results = await new LocalMirrorSyncService().CopyAsync(
            source, fixture.Context, new MirrorSyncSettings { BatchSize = 1 }, _ => { });

        var news = results.Single(r => r.Table == "News");
        Assert.Equal(2, news.CopiedRows);
        var rows = await fixture.Context.News.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(new[] { 5, 9 }, rows.Select(n => n.Id));
        Assert.True(rows[0].original);
        Assert.Null(rows[0].Important);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), rows[0].Date);
        Assert.Equal(true, rows[1].Important);
        Assert.Null(rows[1].Year);

        // 保留原 Id 之后，新插入的行要接着最大 Id 往后走。
        fixture.Context.News.Add(new News { Title = "新增" });
        await fixture.Context.SaveChangesAsync();
        Assert.Equal(10, await fixture.Context.News.MaxAsync(n => n.Id));
    }

    [Fact]
    public async Task Sync_Skips_Tables_Missing_At_Source_And_Defaults_Missing_Columns()
    {
        await using var fixture = OpenSzaipaTarget();
        // 来源 Publication 没有 Type/Preface/Signature（对应生产尚未执行加列 SQL）；ExhibitionWork 根本没有这张表。
        var source = new FakeMirrorSource()
            .Table("Publication", new[] { "Id", "TitleCN", "MaxImg", "ReadCount" }, Row(3, "春语", 12, 4));

        var results = await new LocalMirrorSyncService().CopyAsync(
            source, fixture.Context, new MirrorSyncSettings(), _ => { });

        var publication = results.Single(r => r.Table == "Publication");
        Assert.Contains("Type", publication.MissingSourceColumns);
        Assert.Contains("Preface", publication.MissingSourceColumns);
        Assert.Contains("Signature", publication.MissingSourceColumns);
        Assert.DoesNotContain("TitleCN", publication.MissingSourceColumns);
        var copied = await fixture.Context.Publication.AsNoTracking().SingleAsync();
        Assert.Equal("春语", copied.TitleCN);
        Assert.Equal(0, copied.Type);
        Assert.Null(copied.Preface);

        var work = results.Single(r => r.Table == "ExhibitionWork");
        Assert.Equal(0, work.CopiedRows);
        Assert.Contains("来源无此表", work.Status);
        Assert.Equal(0, await fixture.Context.ExhibitionWork.CountAsync());
    }

    [Fact]
    public async Task Sync_Applies_Date_Filter_Only_To_Configured_Log_Tables()
    {
        await using var fixture = OpenSzaipaTarget();
        var today = new DateTime(2026, 10, 26);
        var source = new FakeMirrorSource()
            .Table("AccessData", new[] { "Id", "Date", "Ip" },
                Row(1, today.AddDays(-200), "1.1.1.1"),
                Row(2, today.AddDays(-10), "2.2.2.2"),
                Row(3, today, "3.3.3.3"))
            .Table("News", new[] { "Id", "Title", "Date" },
                Row(1, "旧新闻", today.AddDays(-900)));

        await new LocalMirrorSyncService().CopyAsync(
            source, fixture.Context, new MirrorSyncSettings { LogRetentionDays = 90, Today = today }, _ => { });

        Assert.Equal(new[] { 2, 3 }, (await fixture.Context.AccessData.AsNoTracking().OrderBy(a => a.Id).ToListAsync()).Select(a => a.Id));
        Assert.Equal(1, await fixture.Context.News.CountAsync()); // News 不过滤
        Assert.Equal(new DateTime(2026, 7, 28), source.LastFilter("AccessData")!.Since);
        Assert.Null(source.LastFilter("News"));

        // 0 / null = 全量
        await using var full = OpenSzaipaTarget();
        await new LocalMirrorSyncService().CopyAsync(
            source, full.Context, new MirrorSyncSettings { LogRetentionDays = 0, Today = today }, _ => { });
        Assert.Equal(3, await full.Context.AccessData.CountAsync());
    }

    [Fact]
    public async Task Sync_Skips_Duplicate_Keys_And_Replaces_Null_In_Required_Columns()
    {
        await using var fixture = OpenTongouTarget();
        // 生产 TongouWorks 没有真正的主键约束，id=211 出现两次；NULL 出现在 NOT NULL 列上也不能让同步崩。
        var source = new FakeMirrorSource()
            .Table("TongouWorks", new[] { "id", "Title", "VisityCount" },
                Row(210, "a", 1), Row(211, "b", 2), Row(211, "b-dup", 3), Row(212, "c", null));

        var result = (await new LocalMirrorSyncService().CopyAsync(
            source, fixture.Context, new MirrorSyncSettings { BatchSize = 10 }, _ => { })).Single(r => r.Table == "TongouWorks");

        Assert.Equal(3, result.CopiedRows);
        Assert.Equal(1, result.DuplicateKeysSkipped);
        Assert.Equal(1, result.NullsReplacedWithDefault);
        var rows = await fixture.Context.TongouWorks.AsNoTracking().OrderBy(w => w.id).ToListAsync();
        Assert.Equal("b", rows.Single(w => w.id == 211).Title);
        Assert.Equal(0, rows.Single(w => w.id == 212).VisityCount);
    }

    // ---------- 对生产库「只读」的护栏 ----------

    [Fact]
    public void Every_Production_Statement_Is_A_Plain_Select()
    {
        var filter = new MirrorDateFilter("Date", new DateTime(2026, 1, 1));
        var statements = new[]
        {
            MirrorSql.SelectColumns,
            MirrorSql.BuildCount("News", null),
            MirrorSql.BuildCount("AccessData", filter),
            MirrorSql.BuildBatch("News", new[] { "Id", "Title" }, "Id", null),
            MirrorSql.BuildBatch("AccessData", new[] { "Id", "Date" }, "Id", filter)
        };

        foreach (var sql in statements)
        {
            Assert.StartsWith("SELECT ", sql);
            Assert.DoesNotContain(";", sql);
            foreach (var forbidden in new[] { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "EXEC", "MERGE", "TRUNCATE", "INTO" })
            {
                Assert.DoesNotContain(forbidden, sql.ToUpperInvariant().Replace("INFORMATION_SCHEMA", string.Empty));
            }
        }
    }

    [Fact]
    public void Identifiers_Are_Bracket_Quoted()
    {
        Assert.Equal("[long]", MirrorSql.Quote("long"));
        Assert.Equal("[a]]b]", MirrorSql.Quote("a]b"));
        Assert.Contains("[Id] > @after ORDER BY [Id]", MirrorSql.BuildBatch("T", new[] { "Id" }, "Id", null));
    }

    // ---------- runner 小工具 ----------

    [Fact]
    public void Sync_Arguments_Are_Parsed_With_Defaults()
    {
        Assert.True(LocalMirrorSyncRunner.TryParseArgs(Array.Empty<string>(), out var days, out var only, out var batch, out _));
        Assert.Equal(90, days);
        Assert.Null(only);
        Assert.Equal(500, batch);

        Assert.True(LocalMirrorSyncRunner.TryParseArgs(
            new[] { "--days", "0", "--only", "Tongou", "--batch", "50" }, out days, out only, out batch, out _));
        Assert.Equal(0, days);
        Assert.Equal("tongou", only);
        Assert.Equal(50, batch);

        Assert.False(LocalMirrorSyncRunner.TryParseArgs(new[] { "--days", "abc" }, out _, out _, out _, out var error));
        Assert.Contains("--days", error);
        Assert.False(LocalMirrorSyncRunner.TryParseArgs(new[] { "--only", "other" }, out _, out _, out _, out _));
    }

    [Fact]
    public void Sync_Refuses_To_Replace_A_File_That_Is_Not_Sqlite()
    {
        var notDatabase = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(notDatabase, "do not delete me, I am not a database file");
        Assert.Throws<InvalidOperationException>(() => LocalMirrorSyncRunner.EnsureSafeToReplace(notDatabase));
        Assert.True(File.Exists(notDatabase));

        LocalMirrorSyncRunner.EnsureSafeToReplace(Path.Combine(_directory, "absent.db")); // 不存在：放行

        var real = Path.Combine(_directory, "real.db");
        using (var context = new SzaipaAdminContext(
            new DbContextOptionsBuilder<SzaipaAdminContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = real, Pooling = false }.ToString())
                .Options))
        {
            context.Database.EnsureCreated();
        }

        LocalMirrorSyncRunner.EnsureSafeToReplace(real); // 真 SQLite：放行
    }

    // ---------- helpers ----------

    private ServiceProvider BuildMirrorProvider(out LocalMirrorOptions mirror, params (string, string?)[] extra)
    {
        var szaipaPath = Path.Combine(_directory, "szaipa.db");
        var tongouPath = Path.Combine(_directory, "tongou.db");
        CreateSchema(new SzaipaAdminContext(NewOptions<SzaipaAdminContext>(szaipaPath)));
        CreateSchema(new TongouAdminContext(NewOptions<TongouAdminContext>(tongouPath)));

        var settings = new List<(string, string?)>
        {
            ("environment", "Development"),
            ("LocalMirror:Enabled", "true"),
            ("LocalMirror:SzaipaPath", szaipaPath),
            ("LocalMirror:TongouPath", tongouPath)
        };
        settings.AddRange(extra);

        var configuration = Config(settings.ToArray());
        mirror = LocalMirrorOptions.FromConfiguration(configuration, _directory);
        var services = new ServiceCollection();
        services.AddSzaipaData(configuration);
        return services.BuildServiceProvider();
    }

    private static DbContextOptions<T> NewOptions<T>(string path) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString())
            .Options;

    private static void CreateSchema(DbContext context)
    {
        using (context)
        {
            context.Database.EnsureCreated();
        }
    }

    private static bool IsSqlite(DbContext context) =>
        context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static object?[] Row(params object?[] values) => values;

    private static ContextFixture<SzaipaAdminContext> OpenSzaipaTarget() =>
        Open<SzaipaAdminContext>(options => new SzaipaAdminContext(options));

    private static ContextFixture<TongouAdminContext> OpenTongouTarget() =>
        Open<TongouAdminContext>(options => new TongouAdminContext(options));

    private static ContextFixture<T> Open<T>(Func<DbContextOptions<T>, T> factory) where T : DbContext
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var context = factory(new DbContextOptionsBuilder<T>().UseSqlite(connection).Options);
        context.Database.EnsureCreated();
        return new ContextFixture<T>(context, connection);
    }

    /// <summary>内存假来源：按表名保存列与行，记录每张表最后一次读取用到的日期过滤。</summary>
    private sealed class FakeMirrorSource : IMirrorSource
    {
        private readonly Dictionary<string, (string[] Columns, List<object?[]> Rows)> _tables =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, MirrorDateFilter?> _filters = new(StringComparer.OrdinalIgnoreCase);

        public FakeMirrorSource Table(string name, string[] columns, params object?[][] rows)
        {
            _tables[name] = (columns, rows.ToList());
            return this;
        }

        public MirrorDateFilter? LastFilter(string table) => _filters.GetValueOrDefault(table);

        public Task<IReadOnlyList<string>?> GetColumnsAsync(string table, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>?>(_tables.TryGetValue(table, out var t) ? t.Columns : null);

        public Task<long> CountAsync(string table, MirrorDateFilter? filter, CancellationToken cancellationToken)
        {
            _filters[table] = filter;
            return Task.FromResult((long)Filtered(table, filter).Count());
        }

        public Task<IReadOnlyList<object?[]>> ReadBatchAsync(
            string table,
            IReadOnlyList<string> columns,
            string keyColumn,
            long afterKey,
            int take,
            MirrorDateFilter? filter,
            CancellationToken cancellationToken)
        {
            _filters[table] = filter;
            var (all, _) = _tables[table];
            var keyIndex = Array.FindIndex(all, c => c.Equals(keyColumn, StringComparison.OrdinalIgnoreCase));
            var picked = columns.Select(c => Array.FindIndex(all, x => x.Equals(c, StringComparison.OrdinalIgnoreCase))).ToArray();

            IReadOnlyList<object?[]> batch = Filtered(table, filter)
                .Where(r => Convert.ToInt64(r[keyIndex]) > afterKey)
                .OrderBy(r => Convert.ToInt64(r[keyIndex]))
                .Take(take)
                .Select(r => picked.Select(i => r[i]).ToArray())
                .ToList();
            return Task.FromResult(batch);
        }

        private IEnumerable<object?[]> Filtered(string table, MirrorDateFilter? filter)
        {
            var (columns, rows) = _tables[table];
            if (filter is null)
            {
                return rows;
            }

            var index = Array.FindIndex(columns, c => c.Equals(filter.Column, StringComparison.OrdinalIgnoreCase));
            return rows.Where(r => r[index] is DateTime date && date >= filter.Since);
        }
    }
}
