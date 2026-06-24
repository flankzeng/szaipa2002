using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>Company (member enterprise) CRUD, replacing the legacy <c>StaffController</c> Company* actions.</summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Company")]
public sealed class CompanyController : Controller
{
    private const int PageSize = 20;

    private readonly ICompanyAdminRepository _repository;

    public CompanyController(ICompanyAdminRepository repository)
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
    public IActionResult Create() => View(new CompanyFormViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "会员企业已添加。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var company = await _repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound();
        }

        return View(ToViewModel(company));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CompanyFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "会员企业资料已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "会员企业已删除。" : "未找到该企业。";
        return RedirectToAction(nameof(Index));
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static Company ToEntity(CompanyFormViewModel model) => new()
    {
        Id = model.Id,
        NameCN = model.NameCN,
        NameEN = model.NameEN,
        CEO = model.CEO,
        Business = model.Business,
        Address = model.Address,
        ImgPath = model.ImgPath
    };

    private static CompanyFormViewModel ToViewModel(Company company) => new()
    {
        Id = company.Id,
        NameCN = company.NameCN,
        NameEN = company.NameEN,
        CEO = company.CEO,
        Business = company.Business,
        Address = company.Address,
        ImgPath = company.ImgPath
    };
}
