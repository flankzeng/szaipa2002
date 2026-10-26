namespace Szaipa.Data.Configuration;

/// <summary>
/// 微信公众号同步配置（配置段 <c>WeChat</c>）。凭据（AppSecret）只允许出现在 gitignore 的
/// <c>appsettings.Local.json</c> / user-secrets / 环境变量里，绝不进仓库。
/// </summary>
public sealed class WeChatOptions
{
    public const string SectionName = "WeChat";

    /// <summary>官方接口通道总开关。链接导入不依赖它（只要后台写库可用即可）。</summary>
    public bool Enabled { get; set; }

    public string AppId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    /// <summary>定时增量同步间隔（分钟）。小于 5 按 5 处理，避免打爆接口。</summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 旧手录新闻的分界 Id：<c>News.Id &lt;= LegacyNewsMaxId</c> 且没有同步记录的新闻从前台列表隐藏
    /// （详情页仍可访问，数据不删）。未配置（null/0）= 不隐藏任何旧新闻。上线时由用户设为切换时刻的最大 News.Id。
    /// </summary>
    public int? LegacyNewsMaxId { get; set; }

    /// <summary>微信接口根地址。仅测试/代理用，默认官方域名。</summary>
    public string ApiBaseUrl { get; set; } = "https://api.weixin.qq.com";

    /// <summary>
    /// 同步进来的文章是否标为"重要新闻"（News.Important = true）。默认 false：列表里是日期+标题的普通行，
    /// 在后台"置顶"后才变成带封面的大卡片，和手录新闻的使用习惯一致。
    /// </summary>
    public bool NewArticlesImportant { get; set; }

    /// <summary>允许下载图片的域名后缀（防 SSRF：文章内容里的 img 指向内网/任意站点一律丢弃）。</summary>
    public List<string> AllowedImageHosts { get; set; } =
        new() { "qpic.cn", "qlogo.cn", "qq.com", "gtimg.cn", "weixin.qq.com" };

    /// <summary>单张图片大小上限（字节）。</summary>
    public long MaxImageBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>批量链接导入时，两次抓取文章页之间的间隔（毫秒），对微信公开页保持礼貌，避免触发人机验证。</summary>
    public int FetchDelayMilliseconds { get; set; } = 800;

    /// <summary>清洗规则的可配置扩展，见 <see cref="WeChatCleaningOptions"/>。</summary>
    public WeChatCleaningOptions Cleaning { get; set; } = new();

    /// <summary>接口来源的独立开关（需 <see cref="IsApiConfigured"/> 才生效）。</summary>
    public WeChatSourcesOptions Sources { get; set; } = new();

    /// <summary>群发完成事件回调（默认关闭，见 <see cref="WeChatCallbackOptions"/>）。</summary>
    public WeChatCallbackOptions Callback { get; set; } = new();

    public bool IsApiConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);

    public TimeSpan SyncInterval => TimeSpan.FromMinutes(Math.Max(5, SyncIntervalMinutes));
}

/// <summary>清洗规则扩展：在内置默认规则之上追加（正则，忽略大小写），不能删除内置规则。</summary>
public sealed class WeChatCleaningOptions
{
    /// <summary>追加的"噪声段落"正则：短段落（≤80 字）命中即整段删除。</summary>
    public List<string> ExtraNoisePatterns { get; set; } = new();

    /// <summary>追加的"尾部截断"正则：命中的段落及其之后的全部内容删除（需位于文章后半部分）。</summary>
    public List<string> ExtraTailMarkerPatterns { get; set; } = new();
}

/// <summary>两个官方接口来源各自独立开关；未认证订阅号常见 48001（无权限），关掉对应来源即可。</summary>
public sealed class WeChatSourcesOptions
{
    /// <summary>发布记录：<c>freepublish/batchget</c>。</summary>
    public bool FreePublish { get; set; } = true;

    /// <summary>永久图文素材：<c>material/batchget_material?type=news</c>（含早期群发内容）。</summary>
    public bool Material { get; set; } = true;
}

/// <summary>
/// 群发完成事件回调 <c>/wechat/callback</c>。<b>默认关闭。</b>在公众平台开启「服务器配置」会接管消息处理，
/// 编辑模式下的自动回复与自定义菜单会失效——务必权衡后再开。仅支持明文模式（不支持消息加解密）。
/// </summary>
public sealed class WeChatCallbackOptions
{
    public bool Enabled { get; set; }

    /// <summary>与公众平台「服务器配置」里填写的 Token 一致；只放本机配置/环境变量。</summary>
    public string Token { get; set; } = string.Empty;

    public bool IsActive => Enabled && !string.IsNullOrWhiteSpace(Token);
}
