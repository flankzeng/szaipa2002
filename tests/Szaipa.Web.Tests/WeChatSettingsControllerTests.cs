using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.WeChat;
using Szaipa.Web.Areas.Staff.Controllers;
using Szaipa.Web.Areas.Staff.Models;
using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>后台「公众号接口配置」页：表单校验、fail-closed、AppSecret 只写不回显。不连外网、不连数据库。</summary>
public sealed class WeChatSettingsControllerTests : IDisposable
{
    private const string FakeAppId = "wx0123456789abcdef";
    private const string FakeSecret = "0123456789abcdef0123456789abcdef";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wechat-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string StorePath => Path.Combine(_root, "wechat-credentials.json");

    private (WeChatSettingsController Controller, IWeChatCredentialStore Store, ServiceProvider Provider) Create(bool storeAvailable = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddWeChatEffectiveOptions(configuration, _ => storeAvailable
            ? new WeChatCredentialStoreLocation(StorePath, null)
            : WeChatCredentialStoreLocation.Resolve(_root, null, isDevelopment: false));
        services.AddOptions<AdminWriteOptions>(); // 未配置写库：操作记录只进日志
        services.AddSingleton<Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory, Microsoft.AspNetCore.Mvc.Routing.UrlHelperFactory>();
        var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IWeChatCredentialStore>();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var controller = new WeChatSettingsController(
            store,
            provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>(),
            configuration,
            new ThrowingApiClient(),
            NullLogger<WeChatSettingsController>.Instance)
        {
            ControllerContext = new ControllerContext(new ActionContext(
                httpContext,
                new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor())),
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
        return (controller, store, provider);
    }

    private static WeChatSettingsForm Form(string? appId = FakeAppId, string? secret = FakeSecret, bool enabled = false, int? interval = 60) => new()
    {
        AppId = appId,
        AppSecret = secret,
        Enabled = enabled,
        FreePublish = true,
        Material = true,
        SyncIntervalMinutes = interval
    };

    [Fact]
    public async Task Invalid_input_is_rejected_and_the_secret_is_not_echoed_back()
    {
        var (controller, store, provider) = Create();
        using var _ = provider;
        controller.ModelState.SetModelValue(nameof(WeChatSettingsForm.AppSecret), "not-hex", "not-hex");

        var result = await controller.Save(Form(appId: "wx123", secret: "not-hex", interval: 2), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<WeChatSettingsViewModel>(view.Model);
        Assert.Equal(3, model.Errors.Count);
        Assert.Null(model.Form.AppSecret);
        Assert.False(controller.ModelState.ContainsKey(nameof(WeChatSettingsForm.AppSecret)));
        Assert.False(File.Exists(StorePath));
        Assert.False(store.Current.Exists);
    }

    [Fact]
    public async Task Enabling_sync_requires_credentials()
    {
        var (controller, _, provider) = Create();
        using var _ = provider;

        var result = await controller.Save(Form(appId: null, secret: null, enabled: true), CancellationToken.None);

        var model = Assert.IsType<WeChatSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Contains(model.Errors, error => error.Contains("AppID 和 AppSecret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Production_without_store_path_cannot_save()
    {
        var (controller, store, provider) = Create(storeAvailable: false);
        using var _ = provider;

        var index = Assert.IsType<WeChatSettingsViewModel>(Assert.IsType<ViewResult>(controller.Index()).Model);
        Assert.False(index.Location.IsAvailable);

        var result = await controller.Save(Form(), CancellationToken.None);

        var model = Assert.IsType<WeChatSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Contains(model.Errors, error => error.Contains("未配置存储位置", StringComparison.Ordinal));
        Assert.False(store.Current.Exists);
        Assert.IsType<RedirectToActionResult>(await controller.ClearSecret(CancellationToken.None));
        Assert.Contains("未配置存储位置", controller.TempData["Error"] as string);
    }

    [Fact]
    public async Task Valid_save_takes_effect_immediately_and_the_page_only_shows_the_last_four()
    {
        var (controller, store, provider) = Create();
        using var _ = provider;

        var result = await controller.Save(Form(enabled: true), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>().CurrentValue.IsApiConfigured);
        Assert.DoesNotContain(FakeSecret, await File.ReadAllTextAsync(StorePath));

        var model = Assert.IsType<WeChatSettingsViewModel>(Assert.IsType<ViewResult>(controller.Index()).Model);
        Assert.Equal("cdef", model.Stored.AppSecretLast4);
        Assert.Equal("本页保存", model.EffectiveSecretSource);
        Assert.Null(model.Form.AppSecret);
        Assert.Equal(FakeAppId, model.Form.AppId);

        Assert.IsType<RedirectToActionResult>(await controller.ClearSecret(CancellationToken.None));
        Assert.False(store.Current.HasAppSecret);
        Assert.False(provider.GetRequiredService<IOptionsMonitor<WeChatOptions>>().CurrentValue.IsApiConfigured);
    }

    private sealed class ThrowingApiClient : IWeChatApiClient
    {
        public Task VerifyCredentialsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WeChatSourcePage> GetPublishedPageAsync(int offset, int count, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WeChatSourcePage> GetMaterialNewsPageAsync(int offset, int count, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WeChatMaterialCount> GetMaterialCountAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
