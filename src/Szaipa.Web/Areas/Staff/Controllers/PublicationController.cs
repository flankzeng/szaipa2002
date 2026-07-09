using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;
using Szaipa.Web.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// Exhibition (Publication) CRUD plus the numbered-gallery image manager and the optional works-catalog
/// (参展作品目录) manager. New exhibitions created here render automatically at the public
/// <c>/Home/Publication/{id}</c> (no more hand-coded slug views).
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Publication")]
public sealed class PublicationController : Controller
{
    private const int PageSize = 20;

    private static readonly JsonSerializerOptions WorksJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IPublicationAdminRepository _repository;
    private readonly IExhibitionGalleryStorage _gallery;
    private readonly IExhibitionWorkAdminRepository _works;

    public PublicationController(
        IPublicationAdminRepository repository,
        IExhibitionGalleryStorage gallery,
        IExhibitionWorkAdminRepository works)
    {
        _repository = repository;
        _gallery = gallery;
        _works = works;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        return View(await _repository.GetPagedAsync(page, PageSize, cancellationToken));
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(new PublicationFormViewModel { Status = true });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PublicationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var id = await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        await SyncGalleryAsync(id, model, cancellationToken);
        await SyncWorksAsync(id, model, cancellationToken);
        TempData["Success"] = "展览已创建，公开页 /Home/Publication/" + id + " 即可访问。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var publication = await _repository.GetByIdAsync(id, cancellationToken);
        if (publication is null)
        {
            return NotFound();
        }

        return View(ToViewModel(publication));
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PublicationFormViewModel model, CancellationToken cancellationToken)
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

        await SyncGalleryAsync(id, model, cancellationToken);
        await SyncWorksAsync(id, model, cancellationToken);
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

    /// <summary>Stages a gallery image upload; returns its (not-yet-numbered) name + preview url.</summary>
    [HttpPost("UploadImage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(string folder, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!_gallery.IsWritable)
        {
            return Json(new { success = false, error = "未配置可写的内容目录（LegacyAssets:ContentRoot）。" });
        }

        if (file is null || string.IsNullOrWhiteSpace(folder))
        {
            return Json(new { success = false, error = "缺少文件或文件夹名。" });
        }

        var image = await _gallery.AddUploadAsync(folder, file, cancellationToken);
        return image is null
            ? Json(new { success = false, error = "上传失败（格式或大小不符）。" })
            : Json(new { success = true, name = image.Name, url = image.Url });
    }

    /// <summary>Existing gallery images for an exhibition folder (used to populate the edit page).</summary>
    [HttpGet("Images")]
    public IActionResult Images(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Json(Array.Empty<object>());
        }

        var images = _gallery.ListGallery(folder).Select(i => new { name = i.Name, url = i.Url });
        return Json(images);
    }

    /// <summary>Existing works-catalog rows for an exhibition (used to populate the edit page's works manager).</summary>
    [HttpGet("Works/{id:int}")]
    public async Task<IActionResult> Works(int id, CancellationToken cancellationToken)
    {
        var works = await _works.GetByPublicationAsync(id, cancellationToken);
        return Json(works.Select(w => new
        {
            id = w.Id,
            category = w.Category,
            title = w.Title,
            artist = w.Artist,
            size = w.Size,
            medium = w.Medium,
            imagePath = w.ImagePath
        }));
    }

    private async Task SyncWorksAsync(int id, PublicationFormViewModel model, CancellationToken cancellationToken)
    {
        // Null means the works-manager component wasn't touched (e.g. its hidden field wasn't submitted) —
        // leave whatever catalog already exists alone. An explicit "[]" (all rows removed in the UI) clears it.
        if (model.WorksJson is null)
        {
            return;
        }

        List<ExhibitionWorkInput>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<ExhibitionWorkInput>>(model.WorksJson, WorksJsonOptions);
        }
        catch (JsonException)
        {
            items = null;
        }

        await _works.ReplaceAsync(id, items ?? new List<ExhibitionWorkInput>(), CurrentActor(), cancellationToken);
    }

    private async Task SyncGalleryAsync(int id, PublicationFormViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.FolderName) || !_gallery.IsWritable)
        {
            return;
        }

        var ordered = (model.GalleryOrder ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var count = _gallery.ApplyOrder(model.FolderName, ordered);
        // MaxImg is the inclusive offset for the frontend loop (10001 .. 10001 + MaxImg) over `count` images.
        await _repository.SetMaxImgAsync(id, count - 1, cancellationToken);
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }

    private static Publication ToEntity(PublicationFormViewModel model) => new()
    {
        Id = model.Id,
        TitleCN = model.TitleCN,
        TitleEN = model.TitleEN,
        StartDate = model.StartDate,
        EndDate = model.EndDate,
        FolderName = model.FolderName,
        Location = model.Location,
        Status = model.Status,
        zhuban = model.zhuban,
        chengban = model.chengban,
        xieban = model.xieban,
        Type = model.Type,
        Preface = model.Preface,
        Signature = model.Signature,
        CoverPath = model.CoverPath
    };

    private static PublicationFormViewModel ToViewModel(Publication publication) => new()
    {
        Id = publication.Id,
        TitleCN = publication.TitleCN,
        TitleEN = publication.TitleEN,
        StartDate = publication.StartDate,
        EndDate = publication.EndDate,
        FolderName = publication.FolderName,
        Location = publication.Location,
        Status = publication.Status,
        zhuban = publication.zhuban,
        chengban = publication.chengban,
        xieban = publication.xieban,
        Type = publication.Type,
        Preface = publication.Preface,
        Signature = publication.Signature,
        CoverPath = publication.CoverPath
    };
}
