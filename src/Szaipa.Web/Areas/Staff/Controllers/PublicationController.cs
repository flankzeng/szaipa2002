using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Composition;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services;
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
    private readonly IPublicationBlockAdminRepository _blocks;
    private readonly SzaipaAdminContext _db;

    public PublicationController(
        IPublicationAdminRepository repository,
        IExhibitionGalleryStorage gallery,
        IExhibitionWorkAdminRepository works,
        IPublicationBlockAdminRepository blocks,
        SzaipaAdminContext db)
    {
        _repository = repository;
        _gallery = gallery;
        _works = works;
        _blocks = blocks;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, PublicationLifecycle? status = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        return View(new PublicationIndexViewModel
        {
            Paged = await _repository.GetPagedAsync(page, PageSize, status, today, cancellationToken),
            Counts = await _repository.GetLifecycleCountsAsync(today, cancellationToken),
            Filter = status,
            Today = today
        });
    }

    [HttpGet("Create")]
    public IActionResult Create() => View(new PublicationFormViewModel { Status = true });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PublicationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!TryParseBlocks(model, out var blockItems) || !ModelState.IsValid)
        {
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
            await SyncGalleryAsync(id, model, cancellationToken);
            await SyncWorksAsync(id, model, cancellationToken);
            await SyncBlocksAsync(id, model, blockItems, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["Success"] = "展览已创建，公开页 /Home/Publication/" + id + " 即可访问。";
            return RedirectToAction(nameof(Index));
        }
        catch (DbException exception) when (IsMissingBlockSchema(exception) && model.Type == PublicationTypes.Topic)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, "专题积木数据表或字段尚未创建，请先执行展览自动化 SQL。");
            return View(model);
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingColumn(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, "展览表结构尚未升级，请先执行展览自动化 SQL 后再创建或保存。");
            return View(model);
        }
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var publication = await _repository.GetByIdAsync(id, cancellationToken);
        if (publication is null)
        {
            return NotFound();
        }

        var model = ToViewModel(publication);
        try
        {
            model.BlocksJson = JsonSerializer.Serialize((await _blocks.GetByPublicationAsync(id, cancellationToken))
                .Select(b => new { type = b.BlockType, content = ParseJsonElement(b.Content) }));
        }
        catch (DbException exception) when (IsMissingBlockSchema(exception))
        {
            model.BlocksJson = "[]";
        }
        return View(model);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PublicationFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!TryParseBlocks(model, out var blockItems) || !ModelState.IsValid)
        {
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
            if (!updated)
            {
                await transaction.RollbackAsync(cancellationToken);
                return NotFound();
            }

            await SyncGalleryAsync(id, model, cancellationToken);
            await SyncWorksAsync(id, model, cancellationToken);
            await SyncBlocksAsync(id, model, blockItems, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["Success"] = "展览已更新。";
            return RedirectToAction(nameof(Index));
        }
        catch (DbException exception) when (IsMissingBlockSchema(exception) && model.Type == PublicationTypes.Topic)
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, "专题积木数据表或字段尚未创建，请先执行展览自动化 SQL。");
            return View(model);
        }
        catch (DbException exception) when (SchemaCompatibility.IsMissingColumn(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            ModelState.AddModelError(string.Empty, "展览表结构尚未升级，请先执行展览自动化 SQL 后再创建或保存。");
            return View(model);
        }
    }

    private bool TryParseBlocks(PublicationFormViewModel model, out IReadOnlyList<PublicationBlockInput> inputs)
    {
        inputs = Array.Empty<PublicationBlockInput>();
        if (model.Type != PublicationTypes.Topic || string.IsNullOrWhiteSpace(model.BlocksJson)) return true;
        try
        {
            var values = JsonSerializer.Deserialize<List<JsonElement>>(model.BlocksJson);
            if (values is null || values.Count > PublicationBlockAdminRepository.MaxBlocksPerPublication)
            {
                ModelState.AddModelError(nameof(model.BlocksJson), "专题积木数量超出上限或格式不正确。");
                return false;
            }

            var parsed = new List<PublicationBlockInput>(values.Count);
            foreach (var value in values)
            {
                if (value.ValueKind != JsonValueKind.Object
                    || !value.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || !PublicationBlockTypes.IsKnown(type.GetString())
                    || !value.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Object)
                {
                    ModelState.AddModelError(nameof(model.BlocksJson), "专题积木包含无效类型或内容。");
                    return false;
                }

                parsed.Add(new PublicationBlockInput(type.GetString(), content.GetRawText()));
            }

            inputs = parsed;
            return true;
        }
        catch (JsonException)
        {
            ModelState.AddModelError(nameof(model.BlocksJson), "专题积木内容不是有效的 JSON。");
            return false;
        }
    }

    private async Task SyncBlocksAsync(int id, PublicationFormViewModel model, IReadOnlyList<PublicationBlockInput> inputs, CancellationToken cancellationToken)
    {
        if (model.Type == PublicationTypes.Topic)
            await _blocks.ReplaceAsync(id, inputs, CurrentActor(), cancellationToken);
    }

    private static bool IsMissingBlockSchema(DbException exception) =>
        SchemaCompatibility.IsMissingTable(exception, "PublicationBlock")
        || SchemaCompatibility.IsMissingColumn(exception);

    private static JsonElement ParseJsonElement(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
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
        CoverPath = model.CoverPath,
        ThemeColor = model.ThemeColor,
        BannerPath = model.BannerPath
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
        CoverPath = publication.CoverPath,
        ThemeColor = publication.ThemeColor,
        BannerPath = publication.BannerPath
    };
}
