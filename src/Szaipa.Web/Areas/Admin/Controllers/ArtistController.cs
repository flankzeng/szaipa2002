using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>
/// Artist CRUD for the staff backend, replacing the legacy <c>StaffController</c> ArtAdd/ArtEdit/ArtDelete
/// actions. Artist is the master record that Works/Fav/Auction/Exhibition/ArtNews all key off of.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Artist")]
public sealed class ArtistController : Controller
{
    private const int PageSize = 20;

    private readonly IArtistAdminRepository _repository;

    public ArtistController(IArtistAdminRepository repository)
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
    public IActionResult Create() => View(new ArtistFormViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArtistFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "会员已添加。";
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
    public async Task<IActionResult> Edit(int id, ArtistFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "会员资料已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "会员已删除。" : "未找到该会员。";
        return RedirectToAction(nameof(Index));
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static Artist ToEntity(ArtistFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistNameCN = model.ArtistNameCN,
        ArtistNameEN = model.ArtistNameEN,
        Sex = model.Sex,
        Nation = model.Nation,
        City = model.City,
        Title = model.Title,
        Position = model.Position,
        Color1 = model.Color1,
        Color2 = model.Color2,
        Introduction = model.Introduction,
        Honor = model.Honor,
        DeedsThings = model.DeedsThings,
        Path = model.Path,
        Path1 = model.Path1,
        Path2 = model.Path2
    };

    private static ArtistFormViewModel ToViewModel(Artist artist) => new()
    {
        Id = artist.Id,
        ArtistNameCN = artist.ArtistNameCN,
        ArtistNameEN = artist.ArtistNameEN,
        Sex = artist.Sex,
        Nation = artist.Nation,
        City = artist.City,
        Title = artist.Title,
        Position = artist.Position,
        Color1 = artist.Color1,
        Color2 = artist.Color2,
        Introduction = artist.Introduction,
        Honor = artist.Honor,
        DeedsThings = artist.DeedsThings,
        Path = artist.Path,
        Path1 = artist.Path1,
        Path2 = artist.Path2
    };
}
