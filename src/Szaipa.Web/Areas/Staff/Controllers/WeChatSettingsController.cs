using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.WeChat;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// 后台「公众号接口配置」（T9）：接入指引 + AppID/AppSecret 等的人工录入（AppSecret 经 Data Protection 加密落盘、只写不回显）
/// + 测试连接（用当前有效配置跑 <see cref="WeChatProbe"/>）。保存后经 <c>IOptionsMonitor</c> 立即生效，无需重启。
/// 存储位置不可用（正式环境未配置 <c>WeChat:CredentialStorePath</c>）时只读展示、不可保存。
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/WeChat/Settings")]
public sealed class WeChatSettingsController : Controller
{
    private readonly IWeChatCredentialStore _store;
    private readonly IOptionsMonitor<WeChatOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly IWeChatApiClient _apiClient;
    private readonly ILogger<WeChatSettingsController> _logger;

    public WeChatSettingsController(
        IWeChatCredentialStore store,
        IOptionsMonitor<WeChatOptions> options,
        IConfiguration configuration,
        IWeChatApiClient apiClient,
        ILogger<WeChatSettingsController> logger)
    {
        _store = store;
        _options = options;
        _configuration = configuration;
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View(BuildModel(form: null, errors: null, probeSteps: null));

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WeChatSettingsForm form, CancellationToken cancellationToken)
    {
        // AppSecret 只写：无论成功失败，都不让它回到页面（ModelState 里的原值也一并清掉）。
        ModelState.Remove(nameof(WeChatSettingsForm.AppSecret));

        var errors = new List<string>();
        if (!_store.Location.IsAvailable)
        {
            errors.Add("未配置存储位置，无法保存：" + _store.Location.Problem);
        }

        if (!ModelState.IsValid)
        {
            errors.Add("同步间隔 / 旧新闻分界 Id 须填写整数。");
        }

        var update = new WeChatSettingsUpdate
        {
            AppId = form.AppId?.Trim(),
            NewAppSecret = form.AppSecret?.Trim(),
            Enabled = form.Enabled,
            FreePublish = form.FreePublish,
            Material = form.Material,
            SyncIntervalMinutes = form.SyncIntervalMinutes ?? 0,
            LegacyNewsMaxId = form.LegacyNewsMaxId
        };
        errors.AddRange(WeChatSettingsValidator.Validate(update));

        var effective = _options.CurrentValue;
        var willHaveAppId = !string.IsNullOrWhiteSpace(update.AppId) || !string.IsNullOrWhiteSpace(ConfiguredOptions().AppId);
        var willHaveSecret = !string.IsNullOrEmpty(update.NewAppSecret) || !string.IsNullOrEmpty(effective.AppSecret);
        if (update.Enabled && (!willHaveAppId || !willHaveSecret))
        {
            errors.Add("开启自动同步前需先填写 AppID 和 AppSecret。");
        }

        if (errors.Count > 0)
        {
            form.AppSecret = null;
            return View("Index", BuildModel(form, errors, probeSteps: null));
        }

        await _store.SaveAsync(update, CurrentStaffName(), cancellationToken);
        await RecordAsync($"更新了公众号接口配置（AppID {WeChatSecretMask.MaskAppId(_options.CurrentValue.AppId)}"
                          + (string.IsNullOrEmpty(update.NewAppSecret) ? "）" : "，已更换 AppSecret）"), cancellationToken);
        TempData["Success"] = "已加密保存，立即生效（无需重启）。建议点「测试连接」确认。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ClearSecret")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearSecret(CancellationToken cancellationToken)
    {
        if (!_store.Location.IsAvailable)
        {
            TempData["Error"] = "未配置存储位置：" + _store.Location.Problem;
            return RedirectToAction(nameof(Index));
        }

        await _store.ClearAppSecretAsync(CurrentStaffName(), cancellationToken);
        await RecordAsync($"清除了公众号 AppSecret（AppID {WeChatSecretMask.MaskAppId(_options.CurrentValue.AppId)}）", cancellationToken);
        TempData["Success"] = string.IsNullOrEmpty(_options.CurrentValue.AppSecret)
            ? "已清除本页保存的 AppSecret。官方接口同步随之停用。"
            : "已清除本页保存的 AppSecret，现在沿用配置文件 / 环境变量里的密钥。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        if (!_store.Location.IsAvailable)
        {
            TempData["Error"] = "未配置存储位置：" + _store.Location.Problem;
            return RedirectToAction(nameof(Index));
        }

        await _store.ResetAsync(cancellationToken);
        await RecordAsync("清空了本页保存的公众号接口配置（恢复为配置文件）", cancellationToken);
        TempData["Success"] = "已清空本页保存的全部设置，恢复为配置文件 / 环境变量的值。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>用当前<b>已保存</b>的有效配置测试连接（不读表单里尚未保存的值）。</summary>
    [HttpPost("Test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        IReadOnlyList<WeChatProbeStep> steps;
        try
        {
            steps = await WeChatProbe.RunAsync(_apiClient, _options.CurrentValue, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            steps = new[] { new WeChatProbeStep("连接", false, "连接微信接口超时，请检查服务器能否访问 api.weixin.qq.com。", null) };
        }

        return View("Index", BuildModel(form: null, errors: null, steps));
    }

    private WeChatSettingsViewModel BuildModel(
        WeChatSettingsForm? form, IReadOnlyList<string>? errors, IReadOnlyList<WeChatProbeStep>? probeSteps)
    {
        var stored = _store.Current;
        var effective = _options.CurrentValue;
        var configured = ConfiguredOptions();

        form ??= new WeChatSettingsForm
        {
            AppId = stored.AppId ?? effective.AppId,
            Enabled = effective.Enabled,
            FreePublish = effective.Sources.FreePublish,
            Material = effective.Sources.Material,
            SyncIntervalMinutes = effective.SyncIntervalMinutes,
            LegacyNewsMaxId = stored.LegacyNewsMaxId
        };

        return new WeChatSettingsViewModel
        {
            Location = _store.Location,
            Stored = stored,
            Form = form,
            EffectiveAppId = effective.AppId,
            EffectiveSecretSource = stored.HasAppSecret
                ? "本页保存"
                : !string.IsNullOrEmpty(configured.AppSecret) ? "配置文件 / 环境变量" : null,
            EffectiveEnabled = effective.Enabled,
            EffectiveApiConfigured = effective.IsApiConfigured,
            EffectiveFreePublish = effective.Sources.FreePublish,
            EffectiveMaterial = effective.Sources.Material,
            EffectiveSyncIntervalMinutes = (int)effective.SyncInterval.TotalMinutes,
            ConfiguredLegacyNewsMaxId = configured.LegacyNewsMaxId,
            EffectiveLegacyNewsMaxId = effective.LegacyNewsMaxId,
            Errors = errors ?? Array.Empty<string>(),
            ProbeSteps = probeSteps,
            WhitelistIp = probeSteps?.Select(step => step.CallerIp).FirstOrDefault(ip => !string.IsNullOrEmpty(ip))
        };
    }

    /// <summary>只来自配置文件 / 环境变量的值（不含本页覆盖），用于区分密钥来源与占位提示。</summary>
    private WeChatOptions ConfiguredOptions()
    {
        var options = new WeChatOptions();
        _configuration.GetSection(WeChatOptions.SectionName).Bind(options);
        return options;
    }

    private string CurrentStaffName() => User.Identity?.Name ?? "后台";

    /// <summary>审计：只记 AppID 末 4 位，绝不记密钥。后台写库未配置 / 记录失败时只写日志，不影响保存结果。</summary>
    private async Task RecordAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var adminOptions = HttpContext.RequestServices.GetRequiredService<IOptions<AdminWriteOptions>>().Value;
            if (!adminOptions.IsConfigured)
            {
                _logger.LogInformation("公众号接口配置操作（后台写库未配置，未写操作记录）：{Message}", message);
                return;
            }

            var db = HttpContext.RequestServices.GetRequiredService<SzaipaAdminContext>();
            var recorder = HttpContext.RequestServices.GetRequiredService<IOperationRecorder>();
            var id = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : 0;
            await recorder.RecordAsync(db, new AdminActor(id, CurrentStaffName()), message, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("公众号接口配置操作记录写入失败（{Error}）：{Message}", exception.GetType().Name, message);
        }
    }
}
