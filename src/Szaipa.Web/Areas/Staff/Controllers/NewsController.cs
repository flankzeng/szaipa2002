using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// News CRUD for the staff backend, replacing the legacy <c>StaffController</c> News actions. Content is
/// stored as HTML (TipTap); images are uploaded once under unique names. This avoids the legacy bugs where
/// the add action dropped the body and the edit/delete paths hard-deleted other issues' images.
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/News")]
public sealed class NewsController : Controller
{
    private const int PageSize = 20;

    private readonly INewsAdminRepository _repository;

    public NewsController(INewsAdminRepository repository)
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
    public IActionResult Create() => View(new NewsFormViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewsFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "新闻已发布。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var news = await _repository.GetByIdAsync(id, cancellationToken);
        if (news is null)
        {
            return NotFound();
        }

        return View(ToViewModel(news));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NewsFormViewModel model, CancellationToken cancellationToken)
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

        TempData["Success"] = "新闻已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, CurrentActor(), cancellationToken);
        TempData[deleted ? "Success" : "Error"] = deleted ? "新闻已删除。" : "未找到该新闻。";
        return RedirectToAction(nameof(Index));
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static News ToEntity(NewsFormViewModel model) => new()
    {
        Id = model.Id,
        Title = model.Title,
        Subtitle = model.Subtitle,
        Content = model.Content,
        CoverPath = model.CoverPath,
        link = string.IsNullOrWhiteSpace(model.Link) ? null : model.Link.Trim(),
        original = string.IsNullOrWhiteSpace(model.Link),
        Important = model.Important
    };

    private static NewsFormViewModel ToViewModel(News news) => new()
    {
        Id = news.Id,
        Title = news.Title,
        Subtitle = news.Subtitle,
        Content = news.Content,
        CoverPath = news.CoverPath,
        Link = news.link,
        Important = news.Important ?? false
    };
}
