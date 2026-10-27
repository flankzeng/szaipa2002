using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// 后台「公众号接口配置」（T9）：加密存储往返、降级、覆盖优先级、无需重启生效、token 缓存作废、
/// 40164 调用方 IP 解析、正式环境 fail-closed、表单校验。全部离线（假 HttpMessageHandler + 临时目录）。
/// </summary>
public sealed class WeChatCredentialStoreTests : IDisposable
{
    // 格式合法的假凭据（不是任何真实公众号的值）。
    private const string FakeAppId = "wx0123456789abcdef";
    private const string FakeSecret = "0123456789abcdef0123456789abcdef";
    private const string OtherSecret = "fedcba9876543210fedcba9876543210";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wechat-cred-" + Guid.NewGuid().ToString("N"));

    public WeChatCredentialStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string StorePath => Path.Combine(_root, "secure", "wechat-credentials.json");

    private WeChatCredentialStore NewStore(IDataProtectionProvider provider) =>
        new(new WeChatCredentialStoreLocation(StorePath, null), provider, NullLogger<WeChatCredentialStore>.Instance);

    private static WeChatSettingsUpdate Update(string? appId = FakeAppId, string? secret = FakeSecret, bool enabled = false,
        bool freePublish = true, bool material = false, int interval = 30, int? legacyMax = null) => new()
    {
        AppId = appId,
        NewAppSecret = secret,
        Enabled = enabled,
        FreePublish = freePublish,
        Material = material,
        SyncIntervalMinutes = interval,
        LegacyNewsMaxId = legacyMax
    };

    // ------------------------------------------------------------------ 存储位置

    [Fact]
    public void Development_defaults_to_app_data_secure_under_content_root()
    {
        var location = WeChatCredentialStoreLocation.Resolve(_root, null, isDevelopment: true);

        Assert.True(location.IsAvailable);
        Assert.Equal(Path.Combine(_root, "App_Data", "secure", "wechat-credentials.json"), location.FilePath);
    }

    [Theory]
    [InlineData(null, "未配置存储位置")]
    [InlineData("", "未配置存储位置")]
    [InlineData("secure/wechat.json", "绝对路径")]
    public void Production_without_an_absolute_path_fails_closed(string? configured, string expectedProblem)
    {
        var location = WeChatCredentialStoreLocation.Resolve(_root, configured, isDevelopment: false);

        Assert.False(location.IsAvailable);
        Assert.Null(location.FilePath);
        Assert.Contains(expectedProblem, location.Problem);
    }

    [Fact]
    public void Production_rejects_a_path_inside_the_release_directory()
    {
        var release = Path.Combine(_root, "release");
        var inside = WeChatCredentialStoreLocation.Resolve(release, Path.Combine(release, "App_Data", "w.json"), isDevelopment: false);
        var outside = WeChatCredentialStoreLocation.Resolve(release, Path.Combine(_root, "data", "w.json"), isDevelopment: false);

        Assert.False(inside.IsAvailable);
        Assert.Contains("发布目录", inside.Problem);
        Assert.True(outside.IsAvailable);
        Assert.Equal(Path.Combine(_root, "data", "w.json"), outside.FilePath);
    }

    [Fact]
    public async Task Unavailable_store_cannot_save_and_overrides_nothing()
    {
        var store = new WeChatCredentialStore(
            WeChatCredentialStoreLocation.Unavailable("未配置存储位置"), new EphemeralDataProtectionProvider());

        Assert.Same(WeChatStoredSettings.Empty, store.Current);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(Update(), "tester", CancellationToken.None));

        var noDataProtection = new WeChatCredentialStore(new WeChatCredentialStoreLocation(StorePath, null), dataProtectionProvider: null);
        Assert.False(noDataProtection.Location.IsAvailable);
    }

    // ------------------------------------------------------------------ 加密往返

    [Fact]
    public async Task Saved_file_never_contains_the_plain_secret_and_round_trips_through_data_protection()
    {
        var provider = new EphemeralDataProtectionProvider();
        var store = NewStore(provider);

        await store.SaveAsync(Update(legacyMax: 5000), "tester", CancellationToken.None);

        var raw = await File.ReadAllTextAsync(StorePath, Encoding.UTF8);
        Assert.DoesNotContain(FakeSecret, raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("appSecretProtected", raw);
        Assert.Contains(FakeAppId, raw); // AppID 不是机密，明文保存
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(StorePath)!, "*.tmp")); // 原子写入不留临时文件

        // 新实例（模拟重启）用同一套密钥读取。
        var reloaded = NewStore(provider).Current;
        Assert.True(reloaded.Exists);
        Assert.True(reloaded.HasAppSecret);
        Assert.Equal(FakeSecret, reloaded.AppSecret);
        Assert.Equal("cdef", reloaded.AppSecretLast4);
        Assert.Equal(FakeAppId, reloaded.AppId);
        Assert.Equal(5000, reloaded.LegacyNewsMaxId);
        Assert.Equal(30, reloaded.SyncIntervalMinutes);
        Assert.Equal("tester", reloaded.UpdatedBy);
        Assert.DoesNotContain(FakeSecret, reloaded.ToString() ?? string.Empty);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(StorePath));
        }
    }

    [Fact]
    public async Task Different_key_ring_degrades_to_unset_instead_of_failing()
    {
        await NewStore(new EphemeralDataProtectionProvider()).SaveAsync(Update(), "tester", CancellationToken.None);

        var stranger = NewStore(new EphemeralDataProtectionProvider()).Current; // 密钥目录丢失 / 换机

        Assert.True(stranger.AppSecretUnreadable);
        Assert.False(stranger.HasAppSecret);
        Assert.Null(stranger.AppSecretLast4);
        Assert.Equal(FakeAppId, stranger.AppId); // 其余项照常

        var options = new WeChatOptions { AppSecret = "from-config" };
        stranger.ApplyTo(options);
        Assert.Equal("from-config", options.AppSecret); // 解不开就沿用配置文件
    }

    [Fact]
    public async Task Blank_secret_keeps_the_saved_one_and_clear_or_reset_removes_it()
    {
        var store = NewStore(new EphemeralDataProtectionProvider());
        await store.SaveAsync(Update(), "tester", CancellationToken.None);

        await store.SaveAsync(Update(secret: null, enabled: true, interval: 15), "tester", CancellationToken.None);
        Assert.Equal(FakeSecret, store.Current.AppSecret);
        Assert.True(store.Current.Enabled);
        Assert.Equal(15, store.Current.SyncIntervalMinutes);

        await store.SaveAsync(Update(secret: OtherSecret), "tester", CancellationToken.None);
        Assert.Equal(OtherSecret, store.Current.AppSecret);

        await store.ClearAppSecretAsync("tester", CancellationToken.None);
        Assert.False(store.Current.HasAppSecret);
        Assert.Equal(FakeAppId, store.Current.AppId);
        Assert.DoesNotContain("CfDJ8", await File.ReadAllTextAsync(StorePath)); // 密文也已移除

        await store.ResetAsync(CancellationToken.None);
        Assert.False(File.Exists(StorePath));
        Assert.False(store.Current.Exists);
    }

    [Fact]
    public async Task Corrupt_file_is_ignored_and_can_be_overwritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await File.WriteAllTextAsync(StorePath, "{ not json");
        var store = NewStore(new EphemeralDataProtectionProvider());

        Assert.NotNull(store.Current.LoadError);
        var options = new WeChatOptions { AppId = "wxconfig" };
        store.Current.ApplyTo(options);
        Assert.Equal("wxconfig", options.AppId);

        await store.SaveAsync(Update(), "tester", CancellationToken.None);
        Assert.Null(store.Current.LoadError);
        Assert.Equal(FakeAppId, store.Current.AppId);
    }

    // ------------------------------------------------------------------ 有效配置（覆盖优先级 + 无需重启）

    private ServiceProvider BuildProvider(IDictionary<string, string?> config, IDataProtectionProvider dataProtection, HttpMessageHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddSingleton(dataProtection);
        services.AddWeChatEffectiveOptions(configuration, _ => new WeChatCredentialStoreLocation(StorePath, null));
        if (handler is not null)
        {
            services.AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(handler));
            services.AddSingleton<IWeChatApiClient, WeChatApiClient>();
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Stored_values_override_configuration_and_unsaved_items_keep_configuration()
    {
        var config = new Dictionary<string, string?>
        {
            ["WeChat:Enabled"] = "false",
            ["WeChat:AppId"] = "wxconfigconfigconfi",
            ["WeChat:AppSecret"] = "config-secret",
            ["WeChat:SyncIntervalMinutes"] = "60",
            ["WeChat:LegacyNewsMaxId"] = "1234",
            ["WeChat:NewArticlesImportant"] = "true"
        };
        using var provider = BuildProvider(config, new EphemeralDataProtectionProvider());
        var monitor = provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>();
        var store = provider.GetRequiredService<IWeChatCredentialStore>();

        Assert.Equal("wxconfigconfigconfi", monitor.CurrentValue.AppId);
        Assert.Equal("config-secret", monitor.CurrentValue.AppSecret);

        var changes = 0;
        using var subscription = monitor.OnChange(_ => Interlocked.Increment(ref changes));

        // 只存 AppSecret 与开关，AppId / 分界 Id 留空 → 沿用配置文件。
        await store.SaveAsync(Update(appId: null, enabled: true, interval: 20, legacyMax: null), "tester", CancellationToken.None);

        var effective = monitor.CurrentValue; // 同一个进程、不重启
        Assert.Equal(1, changes);
        Assert.Equal("wxconfigconfigconfi", effective.AppId);
        Assert.Equal(FakeSecret, effective.AppSecret);
        Assert.True(effective.Enabled);
        Assert.True(effective.IsApiConfigured);
        Assert.Equal(20, effective.SyncIntervalMinutes);
        Assert.True(effective.Sources.FreePublish);
        Assert.False(effective.Sources.Material);
        Assert.Equal(1234, effective.LegacyNewsMaxId);
        Assert.True(effective.NewArticlesImportant); // 存储不涉及的项永远来自配置

        await store.SaveAsync(Update(secret: null, legacyMax: 0), "tester", CancellationToken.None);
        Assert.Equal(FakeAppId, monitor.CurrentValue.AppId);
        Assert.Equal(0, monitor.CurrentValue.LegacyNewsMaxId); // 0 = 明确不隐藏，覆盖配置
        Assert.Equal(FakeSecret, monitor.CurrentValue.AppSecret);

        await store.ResetAsync(CancellationToken.None);
        Assert.Equal("config-secret", monitor.CurrentValue.AppSecret);
        Assert.False(monitor.CurrentValue.Enabled);
    }

    [Fact]
    public async Task Saving_new_credentials_invalidates_the_cached_access_token_without_restart()
    {
        var tokenRequests = new List<string>();
        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/cgi-bin/stable_token")
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                lock (tokenRequests)
                {
                    tokenRequests.Add(body);
                }

                return FakeHandler.Json($"{{\"access_token\":\"TOKEN-{tokenRequests.Count}\",\"expires_in\":7200}}");
            }

            return FakeHandler.Json("{\"total_count\":0,\"item_count\":0,\"item\":[]}");
        });
        using var provider = BuildProvider(new Dictionary<string, string?>(), new EphemeralDataProtectionProvider(), handler);
        var store = provider.GetRequiredService<IWeChatCredentialStore>();
        var client = provider.GetRequiredService<IWeChatApiClient>();

        await store.SaveAsync(Update(), "tester", CancellationToken.None);
        await client.GetPublishedPageAsync(0, 1, CancellationToken.None);
        await client.GetPublishedPageAsync(0, 1, CancellationToken.None);
        Assert.Single(tokenRequests); // 凭据未变：token 缓存命中

        await store.SaveAsync(Update(secret: OtherSecret), "tester", CancellationToken.None);
        await client.GetPublishedPageAsync(0, 1, CancellationToken.None);

        Assert.Equal(2, tokenRequests.Count); // 新凭据：旧 token 作废，重新获取
        Assert.Contains(OtherSecret, tokenRequests[1]);
        Assert.Contains("TOKEN-2", handler.Requests.Last());

        await store.SaveAsync(Update(secret: null, enabled: true), "tester", CancellationToken.None);
        await client.GetPublishedPageAsync(0, 1, CancellationToken.None);
        Assert.Equal(2, tokenRequests.Count); // 只改开关不影响 token
    }

    [Fact]
    public void Production_without_store_path_still_resolves_options_from_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WeChat:AppId"] = "wxconfigconfigconfi"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddWeChatEffectiveOptions(configuration, _ => WeChatCredentialStoreLocation.Resolve(_root, null, isDevelopment: false));
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IWeChatCredentialStore>().Location.IsAvailable);
        Assert.Equal("wxconfigconfigconfi", provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>().CurrentValue.AppId);
        Assert.False(File.Exists(StorePath));
    }

    // ------------------------------------------------------------------ 40164 调用方 IP

    [Theory]
    [InlineData("invalid ip 47.97.1.2 ipv6 ::ffff:47.97.1.2, not in whitelist rid: 6720-abc", "47.97.1.2")]
    [InlineData("invalid ip 1.2.3.4, not in whitelist", "1.2.3.4")]
    [InlineData("Invalid IP 240e:3b7::1 not in whitelist", "240e:3b7::1")]
    [InlineData("not in whitelist", null)]
    [InlineData("invalid ip abc", null)]
    [InlineData(null, null)]
    public void Caller_ip_is_parsed_from_the_40164_message(string? message, string? expected)
    {
        Assert.Equal(expected, WeChatApiClient.ParseCallerIp(message));
    }

    [Fact]
    public async Task Probe_reports_the_ip_to_whitelist_without_leaking_the_secret()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(
            "{\"errcode\":40164,\"errmsg\":\"invalid ip 203.0.113.9 ipv6 ::ffff:203.0.113.9, not in whitelist rid: x\"}"));
        var options = new WeChatOptions { AppId = FakeAppId, AppSecret = FakeSecret };
        var client = new WeChatApiClient(new FakeHttpClientFactory(handler), WeChatTestSupport.Opts(options));

        var steps = await WeChatProbe.RunAsync(client, options, CancellationToken.None);

        var token = Assert.Single(steps, step => step.Name.StartsWith("access_token", StringComparison.Ordinal));
        Assert.False(token.Ok);
        Assert.Equal(40164, token.ErrorCode);
        Assert.Equal("203.0.113.9", token.CallerIp);
        Assert.Contains("203.0.113.9", token.Detail);
        Assert.All(steps, step => Assert.DoesNotContain(FakeSecret, step.Detail));
    }

    // ------------------------------------------------------------------ 表单校验

    [Theory]
    [InlineData(FakeAppId, FakeSecret, 60, null, 0)]
    [InlineData(FakeAppId, null, 5, 0, 0)]       // 留空 = 不改密钥
    [InlineData(null, null, 60, null, 0)]        // 全部留空 = 沿用配置
    [InlineData("wx123", FakeSecret, 60, null, 1)]
    [InlineData("ab0123456789abcdef", FakeSecret, 60, null, 1)]
    [InlineData("wx0123456789abcde!", FakeSecret, 60, null, 1)]
    [InlineData(FakeAppId, "0123456789abcdef0123456789abcdeg", 60, null, 1)]
    [InlineData(FakeAppId, "0123456789abcdef", 60, null, 1)]
    [InlineData(FakeAppId, FakeSecret, 4, null, 1)]
    [InlineData(FakeAppId, FakeSecret, 60, -1, 1)]
    [InlineData("bad", "bad", 1, -5, 4)]
    public void Settings_form_validation(string? appId, string? secret, int interval, int? legacyMax, int expectedErrors)
    {
        var errors = WeChatSettingsValidator.Validate(Update(appId, secret, interval: interval, legacyMax: legacyMax));

        Assert.Equal(expectedErrors, errors.Count);
        Assert.All(errors, error => Assert.DoesNotContain(FakeSecret, error));
    }

    [Fact]
    public void Masking_shows_only_the_last_four_characters()
    {
        Assert.Equal("****cdef", WeChatSecretMask.MaskAppId(FakeAppId));
        Assert.Equal("未设置", WeChatSecretMask.MaskAppId(null));
    }
}
