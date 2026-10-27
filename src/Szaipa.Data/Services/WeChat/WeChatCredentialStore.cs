using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Szaipa.Data.Configuration;

namespace Szaipa.Data.Services.WeChat;

/// <summary>
/// 公众号接口配置存储文件的位置（<c>WeChat:CredentialStorePath</c>）。只做路径计算，不碰文件系统。
/// <list type="bullet">
///   <item>Development 未配置：<c>{ContentRoot}/App_Data/secure/wechat-credentials.json</c>（App_Data 已 gitignore）；相对路径按 ContentRoot 解析。</item>
///   <item>其他环境：必须是发布目录之外的绝对路径；未配置 / 相对路径 / 位于发布目录内 → 不可用（fail-closed：页面不可保存，站点照常运行，配置只取 appsettings/环境变量）。</item>
/// </list>
/// </summary>
public sealed record WeChatCredentialStoreLocation(string? FilePath, string? Problem)
{
    public const string ConfigurationKey = WeChatOptions.SectionName + ":CredentialStorePath";

    public static readonly string DevelopmentDefaultRelativePath =
        Path.Combine("App_Data", "secure", "wechat-credentials.json");

    public bool IsAvailable => FilePath is not null;

    public static WeChatCredentialStoreLocation Unavailable(string problem) => new(null, problem);

    public static WeChatCredentialStoreLocation Resolve(string contentRoot, string? configuredPath, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            return Unavailable("站点内容根目录未知，无法确定存储位置。");
        }

        var root = Path.GetFullPath(contentRoot);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return isDevelopment
                ? new WeChatCredentialStoreLocation(Path.GetFullPath(Path.Combine(root, DevelopmentDefaultRelativePath)), null)
                : Unavailable("未配置存储位置（WeChat:CredentialStorePath）。正式环境需配置为发布目录之外的绝对路径。");
        }

        var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (!Path.IsPathRooted(expanded))
        {
            return isDevelopment
                ? new WeChatCredentialStoreLocation(Path.GetFullPath(Path.Combine(root, expanded)), null)
                : Unavailable("WeChat:CredentialStorePath 必须是绝对路径。");
        }

        var full = Path.GetFullPath(expanded);
        if (!isDevelopment && IsWithin(full, root))
        {
            return Unavailable("WeChat:CredentialStorePath 不能位于发布目录内（发布切换会丢失文件），请改到发布目录之外。");
        }

        return new WeChatCredentialStoreLocation(full, null);
    }

    private static bool IsWithin(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".")
        {
            return true;
        }

        return !Path.IsPathRooted(relative)
               && relative != ".."
               && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}

/// <summary>
/// 后台设置页保存的公众号配置（已解密的内存快照）。每一项为 null = 未保存，沿用 appsettings/环境变量。
/// <b>AppSecret 只在内存里，绝不进日志 / 页面 / 审计</b>；页面只拿 <see cref="AppSecretLast4"/>。
/// 刻意不用 record：record 自动生成的 ToString 会把密钥打印出来。
/// </summary>
public sealed class WeChatStoredSettings
{
    public static readonly WeChatStoredSettings Empty = new();

    /// <summary>存储文件是否存在。</summary>
    public bool Exists { get; init; }

    /// <summary>文件存在但无法解析时的提示（此时不覆盖任何配置）。</summary>
    public string? LoadError { get; init; }

    public string? AppId { get; init; }

    internal string? AppSecret { get; init; }

    /// <summary>文件里有加密的 AppSecret，但当前 Data Protection 密钥解不开（密钥目录变化/丢失）：按「未设置」处理。</summary>
    public bool AppSecretUnreadable { get; init; }

    public bool? Enabled { get; init; }

    public bool? FreePublish { get; init; }

    public bool? Material { get; init; }

    public int? SyncIntervalMinutes { get; init; }

    public int? LegacyNewsMaxId { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }

    public bool HasAppSecret => !string.IsNullOrEmpty(AppSecret);

    public string? AppSecretLast4 => AppSecret is { Length: > 0 } secret ? secret[^Math.Min(4, secret.Length)..] : null;

    /// <summary>把已保存的项覆盖到 <paramref name="options"/>（没保存的项保持配置文件里的值）。</summary>
    public void ApplyTo(WeChatOptions options)
    {
        if (!string.IsNullOrWhiteSpace(AppId))
        {
            options.AppId = AppId;
        }

        if (HasAppSecret)
        {
            options.AppSecret = AppSecret!;
        }

        if (Enabled is bool enabled)
        {
            options.Enabled = enabled;
        }

        if (FreePublish is bool freePublish)
        {
            options.Sources.FreePublish = freePublish;
        }

        if (Material is bool material)
        {
            options.Sources.Material = material;
        }

        if (SyncIntervalMinutes is int interval)
        {
            options.SyncIntervalMinutes = interval;
        }

        if (LegacyNewsMaxId is int legacyMax)
        {
            options.LegacyNewsMaxId = legacyMax;
        }
    }
}

/// <summary>设置页提交的新值。<see cref="NewAppSecret"/> 为空 = 不修改密钥。不用 record，避免 ToString 泄露密钥。</summary>
public sealed class WeChatSettingsUpdate
{
    /// <summary>空 = 不保存（沿用配置文件）。</summary>
    public string? AppId { get; init; }

    /// <summary>新密钥；null/空 = 保持原值（清除走 <see cref="IWeChatCredentialStore.ClearAppSecretAsync"/>）。</summary>
    public string? NewAppSecret { get; init; }

    public bool Enabled { get; init; }

    public bool FreePublish { get; init; }

    public bool Material { get; init; }

    public int SyncIntervalMinutes { get; init; } = 60;

    /// <summary>null = 沿用配置文件；0 = 不隐藏旧新闻。</summary>
    public int? LegacyNewsMaxId { get; init; }
}

/// <summary>设置页输入校验（AppID / AppSecret 格式、间隔下限）。返回中文错误列表，空 = 通过。</summary>
public static class WeChatSettingsValidator
{
    public const int MinSyncIntervalMinutes = 5;
    public const int MaxSyncIntervalMinutes = 24 * 60;

    private static readonly Regex AppIdPattern = new("^wx[0-9A-Za-z]{16}$", RegexOptions.CultureInvariant);
    private static readonly Regex AppSecretPattern = new("^[0-9A-Fa-f]{32}$", RegexOptions.CultureInvariant);

    public static bool IsValidAppId(string? value) => value is not null && AppIdPattern.IsMatch(value);

    public static bool IsValidAppSecret(string? value) => value is not null && AppSecretPattern.IsMatch(value);

    public static IReadOnlyList<string> Validate(WeChatSettingsUpdate update)
    {
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(update.AppId) && !IsValidAppId(update.AppId.Trim()))
        {
            errors.Add("AppID 格式不对：应以 wx 开头，共 18 位字母或数字（公众号后台「开发者ID(AppID)」原样复制）。");
        }

        if (!string.IsNullOrEmpty(update.NewAppSecret) && !IsValidAppSecret(update.NewAppSecret.Trim()))
        {
            errors.Add("AppSecret 格式不对：应为 32 位十六进制字符（0-9、a-f）。请检查是否多复制了空格或少复制了字符。");
        }

        if (update.SyncIntervalMinutes is < MinSyncIntervalMinutes or > MaxSyncIntervalMinutes)
        {
            errors.Add($"同步间隔须在 {MinSyncIntervalMinutes}～{MaxSyncIntervalMinutes} 分钟之间。");
        }

        if (update.LegacyNewsMaxId is < 0)
        {
            errors.Add("旧新闻分界 Id 不能为负数（留空 = 沿用配置文件，0 = 不隐藏旧新闻）。");
        }

        return errors;
    }
}

/// <summary>公众号接口配置存储（后台设置页写、全站读）。</summary>
public interface IWeChatCredentialStore
{
    WeChatCredentialStoreLocation Location { get; }

    /// <summary>当前已保存的配置（已解密的内存快照，首次访问时从文件加载）。</summary>
    WeChatStoredSettings Current { get; }

    /// <summary>保存（原子写入），立即刷新 <see cref="Current"/> 并触发变更令牌（<c>IOptionsMonitor&lt;WeChatOptions&gt;</c> 随之更新）。</summary>
    Task SaveAsync(WeChatSettingsUpdate update, string updatedBy, CancellationToken cancellationToken);

    /// <summary>只清除已保存的 AppSecret（其他项保留），之后沿用配置文件里的密钥（若有）。</summary>
    Task ClearAppSecretAsync(string updatedBy, CancellationToken cancellationToken);

    /// <summary>删除存储文件，全部恢复为配置文件的值。</summary>
    Task ResetAsync(CancellationToken cancellationToken);

    IChangeToken GetChangeToken();
}

/// <summary>
/// 文件方案的 <see cref="IWeChatCredentialStore"/>：JSON 文件，AppSecret 用 ASP.NET Core Data Protection
/// （purpose <see cref="ProtectorPurpose"/>）加密后存为密文，文件里绝不出现明文密钥。
/// 写入走「临时文件 → 替换」保证原子；非 Windows 上文件权限设为 600。
/// 文件损坏 / 密钥解不开时安全降级为「未设置」，站点照常运行。
/// </summary>
public sealed class WeChatCredentialStore : IWeChatCredentialStore
{
    public const string ProtectorPurpose = "Szaipa.WeChat.Credentials.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // 中文（保存人）原样写出，方便运维直接查看文件；密文本身是 base64url，不受影响。
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    private readonly IDataProtector? _protector;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _loadLock = new();
    private WeChatStoredSettings? _current;
    private CancellationTokenSource _changeSource = new();

    public WeChatCredentialStore(
        WeChatCredentialStoreLocation location,
        IDataProtectionProvider? dataProtectionProvider,
        ILogger<WeChatCredentialStore>? logger = null)
    {
        _logger = logger;
        if (location.IsAvailable && dataProtectionProvider is null)
        {
            location = WeChatCredentialStoreLocation.Unavailable("站点未启用 Data Protection，无法加密保存。");
        }

        Location = location;
        _protector = dataProtectionProvider?.CreateProtector(ProtectorPurpose);
    }

    public WeChatCredentialStoreLocation Location { get; }

    public WeChatStoredSettings Current
    {
        get
        {
            var current = Volatile.Read(ref _current);
            if (current is not null)
            {
                return current;
            }

            lock (_loadLock)
            {
                _current ??= Load();
                return _current;
            }
        }
    }

    public IChangeToken GetChangeToken() => new CancellationChangeToken(Volatile.Read(ref _changeSource).Token);

    public async Task SaveAsync(WeChatSettingsUpdate update, string updatedBy, CancellationToken cancellationToken)
    {
        var path = RequirePath();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            StoredFile existing;
            try
            {
                existing = ReadFile(path) ?? new StoredFile();
            }
            catch (JsonException)
            {
                existing = new StoredFile(); // 损坏的旧文件直接被本次保存覆盖
            }

            var newSecret = update.NewAppSecret?.Trim();
            var protectedSecret = !string.IsNullOrEmpty(newSecret)
                ? _protector!.Protect(newSecret)
                : existing.AppSecretProtected;

            var file = new StoredFile
            {
                Version = 1,
                UpdatedAt = DateTimeOffset.Now,
                UpdatedBy = updatedBy,
                AppId = string.IsNullOrWhiteSpace(update.AppId) ? null : update.AppId.Trim(),
                AppSecretProtected = protectedSecret,
                Enabled = update.Enabled,
                Sources = new StoredSources { FreePublish = update.FreePublish, Material = update.Material },
                SyncIntervalMinutes = update.SyncIntervalMinutes,
                LegacyNewsMaxId = update.LegacyNewsMaxId
            };

            await WriteAtomicAsync(path, JsonSerializer.SerializeToUtf8Bytes(file, JsonOptions), cancellationToken);
            _logger?.LogInformation(
                "公众号接口配置已保存（AppID {AppId}，密钥{SecretState}）。",
                WeChatSecretMask.MaskAppId(file.AppId),
                string.IsNullOrEmpty(newSecret) ? "未修改" : "已更新");
        }
        finally
        {
            _writeLock.Release();
        }

        RefreshAndSignal();
    }

    public async Task ClearAppSecretAsync(string updatedBy, CancellationToken cancellationToken)
    {
        var path = RequirePath();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            StoredFile? existing;
            try
            {
                existing = ReadFile(path);
            }
            catch (JsonException)
            {
                existing = null;
            }

            if (existing is null)
            {
                return; // 没有存储文件 = 本来就没保存过密钥
            }

            existing.AppSecretProtected = null;
            existing.UpdatedAt = DateTimeOffset.Now;
            existing.UpdatedBy = updatedBy;
            await WriteAtomicAsync(path, JsonSerializer.SerializeToUtf8Bytes(existing, JsonOptions), cancellationToken);
            _logger?.LogInformation("已清除后台保存的公众号 AppSecret（AppID {AppId}）。", WeChatSecretMask.MaskAppId(existing.AppId));
        }
        finally
        {
            _writeLock.Release();
            RefreshAndSignal();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        var path = RequirePath();
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            _logger?.LogInformation("公众号接口配置存储文件已删除，恢复为配置文件的值。");
        }
        finally
        {
            _writeLock.Release();
        }

        RefreshAndSignal();
    }

    private string RequirePath() =>
        Location.FilePath ?? throw new InvalidOperationException(Location.Problem ?? "未配置存储位置。");

    private void RefreshAndSignal()
    {
        lock (_loadLock)
        {
            _current = Load();
        }

        var previous = Interlocked.Exchange(ref _changeSource, new CancellationTokenSource());
        previous.Cancel();
    }

    private WeChatStoredSettings Load()
    {
        if (Location.FilePath is not { } path || !File.Exists(path))
        {
            return WeChatStoredSettings.Empty;
        }

        StoredFile? file;
        try
        {
            file = ReadFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.LogWarning("公众号接口配置存储文件无法读取（{Error}），已忽略，沿用配置文件。", exception.GetType().Name);
            return new WeChatStoredSettings { Exists = true, LoadError = "存储文件无法读取或已损坏，已忽略（沿用配置文件）。重新保存一次即可修复。" };
        }

        if (file is null)
        {
            return WeChatStoredSettings.Empty;
        }

        string? secret = null;
        var unreadable = false;
        if (!string.IsNullOrEmpty(file.AppSecretProtected))
        {
            try
            {
                secret = _protector!.Unprotect(file.AppSecretProtected);
            }
            catch (CryptographicException)
            {
                unreadable = true;
                _logger?.LogWarning("公众号 AppSecret 无法解密（Data Protection 密钥可能已变化或丢失），按未设置处理，请在后台重新录入。");
            }
        }

        return new WeChatStoredSettings
        {
            Exists = true,
            AppId = string.IsNullOrWhiteSpace(file.AppId) ? null : file.AppId.Trim(),
            AppSecret = secret,
            AppSecretUnreadable = unreadable,
            Enabled = file.Enabled,
            FreePublish = file.Sources?.FreePublish,
            Material = file.Sources?.Material,
            SyncIntervalMinutes = file.SyncIntervalMinutes,
            LegacyNewsMaxId = file.LegacyNewsMaxId,
            UpdatedAt = file.UpdatedAt,
            UpdatedBy = file.UpdatedBy
        };
    }

    private static StoredFile? ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        return bytes.Length == 0 ? null : JsonSerializer.Deserialize<StoredFile>(bytes, JsonOptions);
    }

    private static async Task WriteAtomicAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            await using (var stream = new FileStream(temp, options))
            {
                await stream.WriteAsync(content, cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>存储文件结构（磁盘格式）。AppSecret 只有密文字段。</summary>
    private sealed class StoredFile
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public string? AppId { get; set; }
        public string? AppSecretProtected { get; set; }
        public bool? Enabled { get; set; }
        public StoredSources? Sources { get; set; }
        public int? SyncIntervalMinutes { get; set; }
        public int? LegacyNewsMaxId { get; set; }
    }

    private sealed class StoredSources
    {
        public bool? FreePublish { get; set; }
        public bool? Material { get; set; }
    }
}

/// <summary>AppID / AppSecret 的展示用遮罩（日志、审计、页面）。</summary>
public static class WeChatSecretMask
{
    /// <summary><c>****abcd</c>（只露末 4 位）；空值返回「未设置」。</summary>
    public static string MaskAppId(string? appId) =>
        string.IsNullOrWhiteSpace(appId) ? "未设置" : "****" + appId.Trim()[^Math.Min(4, appId.Trim().Length)..];

    /// <summary>凭据指纹（只用于判断是否变化，内存使用，不可逆）。</summary>
    internal static string Fingerprint(WeChatOptions options) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            options.ApiBaseUrl + "\n" + options.AppId + "\n" + options.AppSecret)));
}

/// <summary>把存储里的值覆盖到 <see cref="WeChatOptions"/>（appsettings/环境变量绑定之后执行）。</summary>
internal sealed class WeChatStoredSettingsPostConfigure : IPostConfigureOptions<WeChatOptions>
{
    private readonly IWeChatCredentialStore _store;

    public WeChatStoredSettingsPostConfigure(IWeChatCredentialStore store) => _store = store;

    public void PostConfigure(string? name, WeChatOptions options)
    {
        if (string.IsNullOrEmpty(name))
        {
            _store.Current.ApplyTo(options);
        }
    }
}

/// <summary>保存后让 <c>IOptionsMonitor&lt;WeChatOptions&gt;</c> 失效重算，实现无需重启生效。</summary>
internal sealed class WeChatCredentialStoreChangeTokenSource : IOptionsChangeTokenSource<WeChatOptions>
{
    private readonly IWeChatCredentialStore _store;

    public WeChatCredentialStoreChangeTokenSource(IWeChatCredentialStore store) => _store = store;

    public string Name => Microsoft.Extensions.Options.Options.DefaultName;

    public IChangeToken GetChangeToken() => _store.GetChangeToken();
}

/// <summary>「有效公众号配置」的 DI 接线：站点（<c>AddSzaipaData</c>）与 <c>wechat-probe</c> 命令共用。</summary>
public static class WeChatEffectiveOptionsRegistration
{
    /// <summary>
    /// 绑定 <c>WeChat</c> 配置段 → 注册加密存储 → PostConfigure 用存储值覆盖 → 存储变更令牌驱动
    /// <c>IOptionsMonitor&lt;WeChatOptions&gt;</c> 重算。需要宿主已注册 <see cref="IDataProtectionProvider"/>，
    /// 否则存储不可用（只用配置文件的值）。
    /// </summary>
    public static IServiceCollection AddWeChatEffectiveOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, WeChatCredentialStoreLocation> resolveLocation)
    {
        services
            .AddOptions<WeChatOptions>()
            .Bind(configuration.GetSection(WeChatOptions.SectionName));
        services.AddSingleton<IWeChatCredentialStore>(serviceProvider => new WeChatCredentialStore(
            resolveLocation(serviceProvider),
            serviceProvider.GetService<IDataProtectionProvider>(),
            serviceProvider.GetService<ILogger<WeChatCredentialStore>>()));
        services.AddSingleton<IPostConfigureOptions<WeChatOptions>, WeChatStoredSettingsPostConfigure>();
        services.AddSingleton<IOptionsChangeTokenSource<WeChatOptions>, WeChatCredentialStoreChangeTokenSource>();
        return services;
    }
}
