using System.Security.Claims;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>Works (artist artwork) CRUD, replacing the legacy ArtWorks* actions.</summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Works")]
public sealed class WorksController : Controller
{
    private const int PageSize = 20;

    private readonly WorksAdminRepository _repository;
    private readonly IArtistSiteAdminRepository _artistSiteRepository;

    public WorksController(WorksAdminRepository repository, IArtistSiteAdminRepository artistSiteRepository)
    {
        _repository = repository;
        _artistSiteRepository = artistSiteRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int? artistId = null, CancellationToken cancellationToken = default)
    {
        ViewBag.ArtistId = artistId;
        ViewBag.WorksSortSupported = await _artistSiteRepository.SupportsWorksSortAsync(cancellationToken);
        return View(await _repository.GetPagedAsync(page, PageSize, cancellationToken, artistId));
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(int? artistId, CancellationToken cancellationToken)
    {
        await PopulateArtistsAsync(artistId, cancellationToken);
        return View(new WorksFormViewModel { ArtistId = artistId ?? 0 });
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

        try
        {
            await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        }
        catch (DbException)
        {
            ModelState.AddModelError(string.Empty, "后台数据库缺少 Works.Sort，作品未保存。请先执行 docs/sql/2026-10-26-artist-site.sql。");
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }
        TempData["Success"] = "作品已新增。";
        return RedirectToAction(nameof(Index), new { artistId = model.ArtistId == 0 ? (int?)null : model.ArtistId });
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        Works? works;
        try
        {
            works = await _repository.GetByIdAsync(id, cancellationToken);
        }
        catch (DbException)
        {
            TempData["Error"] = "后台数据库缺少 Works.Sort，编辑表单不可用。请先执行 docs/sql/2026-10-26-artist-site.sql。";
            return RedirectToAction(nameof(Index));
        }
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

        bool updated;
        try
        {
            updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        }
        catch (DbException)
        {
            TempData["Error"] = "后台数据库缺少 Works.Sort，作品未修改。请先执行 docs/sql/2026-10-26-artist-site.sql。";
            return RedirectToAction(nameof(Index), new { artistId = model.ArtistId });
        }
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
        bool deleted;
        try
        {
            deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        }
        catch (DbException)
        {
            TempData["Error"] = "后台数据库缺少 Works.Sort，未删除作品。请先执行 docs/sql/2026-10-26-artist-site.sql。";
            return RedirectToAction(nameof(Index));
        }
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
