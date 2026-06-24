using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>Fav (artist collection) CRUD, replacing the legacy ArtFav* actions.</summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Fav")]
public sealed class FavController : Controller
{
    private const int PageSize = 20;

    private readonly FavAdminRepository _repository;

    public FavController(FavAdminRepository repository)
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
        return View(new FavFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FavFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "收藏已新增。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var fav = await _repository.GetByIdAsync(id, cancellationToken);
        if (fav is null)
        {
            return NotFound();
        }

        await PopulateArtistsAsync(fav.ArtistId, cancellationToken);
        return View(ToViewModel(fav));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FavFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "收藏已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "收藏已删除。" : "未找到该收藏。";
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

    private static Fav ToEntity(FavFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        Title = model.Title,
        Creator = model.Creator,
        Year = model.Year,
        Location = model.Location,
        Size = model.Size,
        Material = model.Material,
        Type = model.Type,
        Province = model.Province,
        CollectNumber = model.CollectNumber,
        CoverPath = model.CoverPath
    };

    private static FavFormViewModel ToViewModel(Fav fav) => new()
    {
        Id = fav.Id,
        ArtistId = fav.ArtistId,
        Title = fav.Title,
        Creator = fav.Creator,
        Year = fav.Year,
        Location = fav.Location,
        Size = fav.Size,
        Material = fav.Material,
        Type = fav.Type,
        Province = fav.Province,
        CollectNumber = fav.CollectNumber,
        CoverPath = fav.CoverPath
    };
}
