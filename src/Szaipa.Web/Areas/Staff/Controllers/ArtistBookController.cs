using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>艺术家「出版著作」CRUD（新模块，没有旧后台对应 action）。</summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/ArtistBook")]
public sealed class ArtistBookController : Controller
{
    private const int PageSize = 20;
    private const string SqlFile = "docs/sql/2026-10-26-artist-books.sql";

    private readonly ArtistBookAdminRepository _repository;

    public ArtistBookController(ArtistBookAdminRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int? artistId = null, CancellationToken cancellationToken = default)
    {
        ViewBag.ArtistId = artistId;
        return View(await _repository.GetPagedAsync(page, PageSize, cancellationToken, artistId));
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(int? artistId, CancellationToken cancellationToken)
    {
        await PopulateArtistsAsync(null, cancellationToken);
        return View(new ArtistBookFormViewModel { ArtistId = artistId ?? 0 });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArtistBookFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateArtistsAsync(model.ArtistId, cancellationToken);
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "出版著作已新增。";
        return RedirectToAction(nameof(Index), new { artistId = model.ArtistId == 0 ? (int?)null : model.ArtistId });
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var book = await _repository.GetByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        await PopulateArtistsAsync(book.ArtistId, cancellationToken);
        return View(ToViewModel(book));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ArtistBookFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "出版著作已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "出版著作已删除。" : "未找到该出版著作。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>ArtistBook</c> 是加性新表：在用户执行 <see cref="SqlFile"/> 之前访问本模块不 500，
    /// 而是显示「需先执行 SQL」提示页（前台此时 Art 侧栏为空、档案页显示「暂无出版著作资料」）。
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled && IsMissingSchema(executed.Exception))
        {
            executed.Result = View("_SchemaMissing", new SchemaMissingViewModel("出版著作", nameof(ArtistBook), SqlFile));
            executed.ExceptionHandled = true;
        }
    }

    private static bool IsMissingSchema(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException db
                && (SchemaCompatibility.IsMissingTable(db, nameof(ArtistBook)) || SchemaCompatibility.IsMissingColumn(db)))
            {
                return true;
            }
        }

        return false;
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

    private static ArtistBook ToEntity(ArtistBookFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        Title = model.Title,
        Publisher = model.Publisher,
        PublishYear = model.PublishYear,
        Description = model.Description,
        Sort = model.Sort,
        CoverPath = model.CoverPath
    };

    private static ArtistBookFormViewModel ToViewModel(ArtistBook book) => new()
    {
        Id = book.Id,
        ArtistId = book.ArtistId,
        Title = book.Title,
        Publisher = book.Publisher,
        PublishYear = book.PublishYear,
        Description = book.Description,
        Sort = book.Sort,
        CoverPath = book.CoverPath
    };
}
