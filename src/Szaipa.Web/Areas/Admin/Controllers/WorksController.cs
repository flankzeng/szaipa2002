using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>Works (artist artwork) CRUD, replacing the legacy ArtWorks* actions.</summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Works")]
public sealed class WorksController : Controller
{
    private const int PageSize = 20;

    private readonly WorksAdminRepository _repository;

    public WorksController(WorksAdminRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        return View(await _repository.GetPagedAsync(page, PageSize, cancellationToken));
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateArtistsAsync(null, cancellationToken);
        return View(new WorksFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorksFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "作品已新增。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var works = await _repository.GetByIdAsync(id, cancellationToken);
        if (works is null)
        {
            return NotFound();
        }

        await PopulateArtistsAsync(works.ArtistId, cancellationToken);
        return View(ToViewModel(works));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WorksFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }

        var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        TempData["Success"] = "作品已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "作品已删除。" : "未找到该作品。";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateArtistsAsync(int? selected, CancellationToken cancellationToken)
    {
        var options = await _repository.GetArtistOptionsAsync(cancellationToken);
        ViewBag.Artists = new SelectList(options, nameof(ArtistOption.Id), nameof(ArtistOption.Name), selected);
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static Works ToEntity(WorksFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        Title = model.Title,
        Content = model.Content,
        Tags = model.Tags,
        Path = model.Path
    };

    private static WorksFormViewModel ToViewModel(Works works) => new()
    {
        Id = works.Id,
        ArtistId = works.ArtistId,
        Title = works.Title,
        Content = works.Content,
        Tags = works.Tags,
        Path = works.Path
    };
}
