using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>在售作品 (SaleWork) for the staff backend: the slides of the homepage 在售艺术品 carousel.</summary>
[Route("Staff/SaleWork")]
public sealed class SaleWorkController : SortableBlockControllerBase<SaleWork>
{
    private readonly ISaleWorkAdminRepository _repository;

    public SaleWorkController(ISaleWorkAdminRepository repository)
    {
        _repository = repository;
    }

    protected override ISortableAdminRepository<SaleWork> Repository => _repository;

    protected override string Noun => "在售作品";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _repository.GetAllAsync(cancellationToken));

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View(new SaleWorkFormViewModel { ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken) });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleWorkFormViewModel model, CancellationToken cancellationToken)
    {
        await ValidateAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "在售作品已添加。";
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

        return View(new SaleWorkFormViewModel
        {
            Id = work.Id,
            ArtistId = work.ArtistId,
            ArtistName = work.ArtistName,
            Title = work.Title,
            Size = work.Size,
            Medium = work.Medium,
            ImagePath = work.ImagePath,
            ImageWidth = work.ImageWidth,
            ImageHeight = work.ImageHeight,
            IsVisible = work.IsVisible,
            ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken)
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SaleWorkFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        await ValidateAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        TempData["Success"] = "在售作品已更新。";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(SaleWorkFormViewModel model, CancellationToken cancellationToken)
    {
        model.ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken);
        if (model.ArtistId is { } artistId && model.ArtistOptions.All(a => a.Id != artistId))
        {
            ModelState.AddModelError(nameof(model.ArtistId), "所选艺术家不存在。");
        }
    }

    private static SaleWork ToEntity(SaleWorkFormViewModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        ArtistName = model.ArtistName,
        Title = model.Title ?? string.Empty,
        Size = model.Size,
        Medium = model.Medium,
        ImagePath = model.ImagePath,
        ImageWidth = model.ImageWidth,
        ImageHeight = model.ImageHeight,
        IsVisible = model.IsVisible
    };
}
