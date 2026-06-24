using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>Exhibition (per-artist exhibition feed) CRUD, replacing the legacy ArtExhibition* actions.</summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Exhibition")]
public sealed class ExhibitionController : Controller
{
    private const int PageSize = 20;

    private readonly ExhibitionAdminRepository _repository;

    public ExhibitionController(ExhibitionAdminRepository repository)
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
        return View(new ExhibitionFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExhibitionFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "展览已新增。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var exhibition = await _repository.GetByIdAsync(id, cancellationToken);
        if (exhibition is null)
        {
            return NotFound();
        }

        await PopulateArtistsAsync(exhibition.ArtistId, cancellationToken);
        return View(ToViewModel(exhibition));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ExhibitionFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "展览已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "展览已删除。" : "未找到该展览。";
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

    private static Exhibition ToEntity(ExhibitionFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        Title = model.Title,
        Location = model.Location,
        StartDate = model.StartDate,
        EndDate = model.EndDate,
        Link = model.Link,
        CoverPath = model.CoverPath
    };

    private static ExhibitionFormViewModel ToViewModel(Exhibition exhibition) => new()
    {
        Id = exhibition.Id,
        ArtistId = exhibition.ArtistId,
        Title = exhibition.Title,
        Location = exhibition.Location,
        StartDate = exhibition.StartDate,
        EndDate = exhibition.EndDate,
        Link = exhibition.Link,
        CoverPath = exhibition.CoverPath
    };
}
