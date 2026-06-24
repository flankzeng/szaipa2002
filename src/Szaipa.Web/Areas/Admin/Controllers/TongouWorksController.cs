using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>同构(Tongou) work CRUD, replacing the legacy Project_TongouController WorkAdd/Edit actions.</summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/TongouWorks")]
public sealed class TongouWorksController : Controller
{
    private const int PageSize = 20;

    private readonly ITongouWorksAdminRepository _repository;

    public TongouWorksController(ITongouWorksAdminRepository repository)
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
        return View(new TongouWorksFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TongouWorksFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.Atristid, cancellationToken);
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "同构作品已新增。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var work = await _repository.GetByIdAsync(id, cancellationToken);
        if (work is null)
        {
            return NotFound();
        }

        await PopulateArtistsAsync(work.Atristid, cancellationToken);
        return View(ToViewModel(work));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TongouWorksFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.Atristid, cancellationToken);
            return View(model);
        }

        var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        TempData["Success"] = "同构作品已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "同构作品已删除。" : "未找到该作品。";
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

    private static TongouWorks ToEntity(TongouWorksFormViewModel model) => new()
    {
        id = model.Id,
        Atristid = model.Atristid,
        Title = model.Title,
        Size = model.Size,
        Type = model.Type,
        CreationDate = model.CreationDate,
        ImgPath = model.ImgPath
    };

    private static TongouWorksFormViewModel ToViewModel(TongouWorks work) => new()
    {
        Id = work.id,
        Atristid = work.Atristid,
        Title = work.Title,
        Size = work.Size,
        Type = work.Type,
        CreationDate = work.CreationDate,
        ImgPath = work.ImgPath
    };
}
