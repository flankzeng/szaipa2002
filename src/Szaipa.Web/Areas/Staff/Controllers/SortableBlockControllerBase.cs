using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// Shared actions of the three ordered homepage/About block modules (组织架构 / 在售作品 / 合作伙伴): move
/// up/down, show/hide and delete. Each derived controller adds its own Index/Create/Edit because the lists and
/// forms differ. Every write is POST + anti-forgery and goes through the repository (which logs via
/// <c>IOperationRecorder</c>).
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
public abstract class SortableBlockControllerBase<T> : Controller
    where T : class, ISortableRecord
{
    protected abstract ISortableAdminRepository<T> Repository { get; }

    /// <summary>Display noun for flash messages, e.g. "成员".</summary>
    protected abstract string Noun { get; }

    [HttpPost("Move/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(int id, string direction, CancellationToken cancellationToken)
    {
        var step = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? -1
            : string.Equals(direction, "down", StringComparison.OrdinalIgnoreCase) ? 1
            : 0;
        if (step == 0)
        {
            return BadRequest();
        }

        var moved = await Repository.MoveAsync(id, step, CurrentActor(), cancellationToken);
        if (!moved)
        {
            TempData["Error"] = $"未找到该{Noun}。";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Visible/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Visible(int id, bool visible, CancellationToken cancellationToken)
    {
        var updated = await Repository.SetVisibleAsync(id, visible, CurrentActor(), cancellationToken);
        TempData[updated ? "Success" : "Error"] = updated
            ? (visible ? $"{Noun}已显示。" : $"{Noun}已隐藏。")
            : $"未找到该{Noun}。";
        return RedirectToAction("Index");
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await Repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? $"{Noun}已删除。" : $"未找到该{Noun}。";
        return RedirectToAction("Index");
    }

    protected AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }
}
