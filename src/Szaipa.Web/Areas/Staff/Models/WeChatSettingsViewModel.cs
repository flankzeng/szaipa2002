using Szaipa.Data.Services.WeChat;

namespace Szaipa.Web.Areas.Staff.Models;

/// <summary>
/// 后台「公众号接口配置」页。<b>不含 AppSecret 明文</b>：只有「是否已设置 / 来源 / 末 4 位」。
/// </summary>
public sealed class WeChatSettingsViewModel
{
    public required WeChatCredentialStoreLocation Location { get; init; }

    public required WeChatStoredSettings Stored { get; init; }

    /// <summary>表单回填值（AppSecret 永远不回填）。</summary>
    public required WeChatSettingsForm Form { get; init; }

    // ---- 当前生效（配置文件 + 本页保存的值） ----
    public string EffectiveAppId { get; init; } = string.Empty;

    /// <summary>生效中的 AppSecret 来自哪里：「本页保存」「配置文件 / 环境变量」或 null（未设置）。</summary>
    public string? EffectiveSecretSource { get; init; }

    public bool EffectiveEnabled { get; init; }

    public bool EffectiveApiConfigured { get; init; }

    public bool EffectiveFreePublish { get; init; }

    public bool EffectiveMaterial { get; init; }

    public int EffectiveSyncIntervalMinutes { get; init; }

    /// <summary>仅来自配置文件的分界 Id（输入框留空时生效的值），用作占位提示。</summary>
    public int? ConfiguredLegacyNewsMaxId { get; init; }

    public int? EffectiveLegacyNewsMaxId { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>「测试连接」的逐步结果；非测试请求为 null。</summary>
    public IReadOnlyList<WeChatProbeStep>? ProbeSteps { get; init; }

    /// <summary>测试时微信返回 40164 报出的调用方出口 IP（需加入白名单）。</summary>
    public string? WhitelistIp { get; init; }
}

/// <summary>设置页表单。AppSecret 只写：提交后只用于加密保存，不回显、不进 ModelState 以外的任何地方。</summary>
public sealed class WeChatSettingsForm
{
    public string? AppId { get; set; }

    public string? AppSecret { get; set; }

    public bool Enabled { get; set; }

    public bool FreePublish { get; set; } = true;

    public bool Material { get; set; } = true;

    public int? SyncIntervalMinutes { get; set; } = 60;

    public int? LegacyNewsMaxId { get; set; }
}
