using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>同构(Tongou) artist CRUD, replacing the legacy Project_TongouController AtristAdd/Edit actions.</summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/TongouAtrist")]
public sealed class TongouAtristController : Controller
{
    private const int PageSize = 20;

    private readonly ITongouAtristAdminRepository _repository;

    public TongouAtristController(ITongouAtristAdminRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetPagedAsync(page, PageSize, cancellationToken);
        return View(result);
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(new TongouAtristFormViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TongouAtristFormViewModel model, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model.Name) && await _repository.NameExistsAsync(model.Name, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Name), "已存在此艺术家");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "同构艺术家已添加。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var artist = await _repository.GetByIdAsync(id, cancellationToken);
        if (artist is null)
        {
            return NotFound();
        }

        return View(ToViewModel(artist));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TongouAtristFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        TempData["Success"] = "同构艺术家已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "同构艺术家已删除。" : "未找到该艺术家。";
        return RedirectToAction(nameof(Index));
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static TongouAtrist ToEntity(TongouAtristFormViewModel model) => new()
    {
        id = model.Id,
        Name = model.Name,
        Title = model.Title,
        AboutText = model.AboutText,
        HeardPath = model.HeardPath
    };

    private static TongouAtristFormViewModel ToViewModel(TongouAtrist artist) => new()
    {
        Id = artist.id,
        Name = artist.Name,
        Title = artist.Title,
        AboutText = artist.AboutText,
        HeardPath = artist.HeardPath
    };
}
