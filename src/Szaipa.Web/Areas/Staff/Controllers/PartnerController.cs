using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>合作伙伴 (Partner) for the staff backend: the cards on the About page.</summary>
[Route("Staff/Partner")]
public sealed class PartnerController : SortableBlockControllerBase<Partner>
{
    private readonly IPartnerAdminRepository _repository;

    public PartnerController(IPartnerAdminRepository repository)
    {
        _repository = repository;
    }

    protected override ISortableAdminRepository<Partner> Repository => _repository;

    protected override string Noun => "合作伙伴";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _repository.GetAllAsync(cancellationToken));

    [HttpGet("Create")]
    public IActionResult Create() => View(new PartnerFormViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartnerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "合作伙伴已添加。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        return View(new PartnerFormViewModel
        {
            Id = partner.Id,
            Name = partner.Name,
            LogoPath = partner.LogoPath,
            ImageWidth = partner.ImageWidth,
            ImageHeight = partner.ImageHeight,
            Link = partner.Link,
            Description = partner.Description,
            IsVisible = partner.IsVisible
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PartnerFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "合作伙伴已更新。";
        return RedirectToAction(nameof(Index));
    }

    private static Partner ToEntity(PartnerFormViewModel model) => new()
    {
        Id = model.Id,
        Name = model.Name ?? string.Empty,
        LogoPath = model.LogoPath,
        ImageWidth = model.ImageWidth,
        ImageHeight = model.ImageHeight,
        Link = model.Link,
        Description = model.Description,
        IsVisible = model.IsVisible
    };
}
