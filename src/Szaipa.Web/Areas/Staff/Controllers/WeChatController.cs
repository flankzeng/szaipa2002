using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.WeChat;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// 公众号文章同步后台：把公众号文章按现有新闻结构导入 News 表（前台版式不变）。
/// 同步记录列表、批量链接导入、官方接口立即同步（增量/全量回填）、隐藏/置顶、重新抓取。
/// 导入后的新闻照常在「新闻」模块里继续编辑。同步记录表尚未建立（未执行 SQL）时只显示提示，不报错。
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/WeChat")]
public sealed class WeChatController : Controller
{
    private const int PageSize = 30;

    private readonly IWeChatSyncService _syncService;
    private readonly IWeChatSyncAdminRepository _repository;
    private readonly IOptionsMonitor<WeChatOptions> _options;
    private readonly IOptions<LegacyAssetsOptions> _legacyAssets;
    private readonly IWeChatSourceStatusStore _status;

    public WeChatController(
        IWeChatSyncService syncService,
        IWeChatSyncAdminRepository repository,
        IOptionsMonitor<WeChatOptions> options,
        IOptions<LegacyAssetsOptions> legacyAssets,
        IWeChatSourceStatusStore status)
    {
        _status = status;
        _syncService = syncService;
        _repository = repository;
        _options = options;
        _legacyAssets = legacyAssets;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildModelAsync(page, null, null, cancellationToken));

    /// <summary>批量链接导入（每行一条，最多 50 条）。结果直接在本页逐条展示。</summary>
    [HttpPost("Import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string? links, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(links))
        {
            TempData["Error"] = "请先粘贴至少一条公众号文章链接。";
            return RedirectToAction(nameof(Index));
        }

        var results = await _syncService.ImportLinksAsync(links, CurrentActor(), force: false, cancellationToken);
        return View("Index", await BuildModelAsync(1, links, results, cancellationToken));
    }

    /// <summary>官方接口立即同步：full=true 回填全部历史，否则只看最新两页。</summary>
    [HttpPost("Sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(bool full, CancellationToken cancellationToken)
    {
        var summary = await _syncService.SyncFromApiAsync(full, CurrentActor(), cancellationToken);
        if (summary.Error is not null)
        {
            TempData["Error"] = $"同步未执行：{summary.Error}";
        }
        else
        {
            // 每个来源独立汇报：一个来源失败（48001/40164…）不影响另一个。
            var lines = summary.Sources.Select(r => r.Error is null
                ? $"{WeChatSources.DisplayName(r.SourceId)}：拉取 {r.Fetched}，新增 {r.Created}，更新 {r.Updated}，无变化 {r.Unchanged}，失败 {r.Failed}"
                : $"{WeChatSources.DisplayName(r.SourceId)}：{r.Error}").ToList();
            var anyError = summary.Sources.Any(r => r.Error is not null);
            TempData[anyError ? "Error" : "Success"] = (full ? "全量回填" : "增量同步") + "：" + string.Join("；", lines);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Hide/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id, bool hidden, int page = 1, CancellationToken cancellationToken = default)
    {
        var done = await _repository.SetHiddenAsync(id, hidden, CurrentActor(), cancellationToken);
        TempData[done ? "Success" : "Error"] = done ? (hidden ? "已隐藏，前台列表不再显示（详情链接仍可访问）。" : "已取消隐藏。") : "未找到该同步记录。";
        return RedirectToAction(nameof(Index), new { page });
    }

    [HttpPost("Pin/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pin(int id, bool pinned, int page = 1, CancellationToken cancellationToken = default)
    {
        var done = await _repository.SetPinnedAsync(id, pinned, CurrentActor(), cancellationToken);
        TempData[done ? "Success" : "Error"] = done ? (pinned ? "已置顶（标为重要新闻）。" : "已取消置顶。") : "未找到该同步记录或对应新闻已被删除。";
        return RedirectToAction(nameof(Index), new { page });
    }

    /// <summary>重新抓取：按记录里的文章链接重新解析并覆盖对应新闻（会覆盖后台人工修改）。</summary>
    [HttpPost("Reimport/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reimport(int id, int page = 1, CancellationToken cancellationToken = default)
    {
        var outcome = await _syncService.ReimportAsync(id, CurrentActor(), cancellationToken);
        TempData[outcome.Status == WeChatImportStatus.Failed ? "Error" : "Success"] =
            outcome.Status == WeChatImportStatus.Failed ? $"重新抓取失败：{outcome.Message}" : $"重新抓取完成：{outcome.Title}（{outcome.Message}）";
        return RedirectToAction(nameof(Index), new { page });
    }

    private async Task<WeChatIndexViewModel> BuildModelAsync(
        int page, string? input, IReadOnlyList<WeChatImportOutcome>? results, CancellationToken cancellationToken)
    {
        var root = _legacyAssets.Value.ContentRoot;
        var options = _options.CurrentValue; // 有效配置（配置文件 + 「接口配置」页保存的值）
        return new WeChatIndexViewModel
        {
            List = await _repository.GetPagedAsync(page, PageSize, cancellationToken),
            ApiConfigured = options.IsApiConfigured,
            ContentRootReady = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root),
            LegacyNewsMaxId = options.LegacyNewsMaxId,
            Sources = _status.Snapshot(options),
            ImportInput = input,
            Results = results
        };
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }
}
