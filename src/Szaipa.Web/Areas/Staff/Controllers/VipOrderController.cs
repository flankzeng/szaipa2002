using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// 会员风采（/Home/Vip）排序与显隐。列出全部艺术家（顺序与前台一致），拖拽排序（拖完即保存，另有「保存顺序」按钮兜底）
/// 与显示/隐藏切换。隐藏只影响 Vip 列表，艺术家官网页本身仍可访问。所有写操作 POST + 防伪令牌 + 审计。
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/VipOrder")]
public sealed class VipOrderController : Controller
{
    private const string SqlFile = "docs/sql/2026-10-26-vip-order.sql";

    private readonly IVipOrderAdminRepository _repository;

    public VipOrderController(IVipOrderAdminRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _repository.GetItemsAsync(cancellationToken));
    }

    /// <summary>拖拽/按钮保存顺序：请求体为按新顺序排列的艺术家 Id 数组（JSON）。</summary>
    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] int[] ids, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveOrderAsync(ids ?? Array.Empty<int>(), CurrentActor(), cancellationToken);
            return Ok(new { success = true });
        }
        catch (DbException exception) when (IsMissingSchema(exception))
        {
            return Conflict(new { error = $"数据库缺少 ArtistDisplay 表，请先执行 {SqlFile}。" });
        }
    }

    [HttpPost("Visible/{artistId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Visible(int artistId, bool hidden, CancellationToken cancellationToken)
    {
        var updated = await _repository.SetHiddenAsync(artistId, hidden, CurrentActor(), cancellationToken);
        TempData[updated ? "Success" : "Error"] = updated
            ? (hidden ? "已在会员风采中隐藏该会员。" : "已在会员风采中恢复显示该会员。")
            : "未找到该会员。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>ArtistDisplay</c> 是加性新表：在用户执行 <see cref="SqlFile"/> 之前访问本页不 500，
    /// 而是显示「需先执行 SQL」提示页（前台此时 Vip 页回退原顺序）。
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled && IsMissingSchema(executed.Exception))
        {
            executed.Result = View("_SchemaMissing", new SchemaMissingViewModel("会员风采排序", nameof(ArtistDisplay), SqlFile));
            executed.ExceptionHandled = true;
        }
    }

    private static bool IsMissingSchema(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException db
                && (SchemaCompatibility.IsMissingTable(db, nameof(ArtistDisplay)) || SchemaCompatibility.IsMissingColumn(db)))
            {
                return true;
            }
        }

        return false;
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }
}
