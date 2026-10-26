using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Szaipa.Web.Authorization;
using Szaipa.Web.Services;
using Szaipa.Web.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>艺术家官网的一站式概览、批量作品录入和展讯关联维护。</summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Artist/{artistId:int}/Site")]
public sealed class ArtistSiteController : Controller
{
    private readonly IArtistSiteAdminRepository _repository;
    private readonly IAdminAssetStorage _assetStorage;

    public ArtistSiteController(IArtistSiteAdminRepository repository, IAdminAssetStorage assetStorage)
    {
        _repository = repository;
        _assetStorage = assetStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int artistId, CancellationToken cancellationToken)
    {
        var overview = await _repository.GetOverviewAsync(artistId, cancellationToken);
        if (overview is null)
        {
            return NotFound();
        }

        var exhibitions = await _repository.GetExhibitionLinksAsync(artistId, cancellationToken);

        // 始终合并数据库行与旧站兼容目录；数据库标题优先，避免只新增一条 Publication 就隐藏其余历史展览。
        var publications = (await _repository.GetPublicationOptionsAsync(cancellationToken))
            .Concat(LegacyPublicationOptions())
            .Append(new PublicationOption(
                ArtistExhibitionCompatibilityCatalog.TonggouTwoId,
                "同构《第二季》·当代艺术展（手写专题页）",
                new DateTime(2022, 12, 29),
                "深圳市罗湖美术馆"))
            .GroupBy(option => option.Id)
            .Select(group => group.FirstOrDefault(option => !option.Title.StartsWith("兼容展览 #", StringComparison.Ordinal)) ?? group.First())
            .OrderBy(option => ExhibitionOrder(exhibitions, option.Id))
            .ThenByDescending(option => option.Id)
            .ToArray();

        return View(new Models.ArtistSiteViewModel
        {
            Overview = overview,
            Works = await _repository.GetWorksAsync(artistId, cancellationToken),
            Exhibitions = exhibitions,
            Publications = publications
        });
    }

    [HttpPost("Works/Upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadWorks(int artistId, List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return BadRequest(new { error = "请选择作品图片。" });
        }

        var overview = await _repository.GetOverviewAsync(artistId, cancellationToken);
        if (overview is null)
        {
            return NotFound();
        }

        if (!overview.WorksSortSupported)
        {
            TempData["Error"] = "后台数据库缺少 Works.Sort。作品数据未修改，请先执行 docs/sql/2026-10-26-artist-site.sql。";
            return RedirectToAction(nameof(Index), new { artistId });
        }

        var entries = new List<NewArtistWork>(files.Count);
        foreach (var file in files)
        {
            var saved = await _assetStorage.SaveImageAsync(file, "ArtImg/Artist/works-narrow", cancellationToken);
            if (!saved.Success)
            {
                TempData["Error"] = saved.Error;
                return RedirectToAction(nameof(Index), new { artistId });
            }

            entries.Add(new NewArtistWork(
                saved.FileName!,
                Path.GetFileNameWithoutExtension(file.FileName),
                null));
        }

        await _repository.AddWorksAsync(artistId, entries, CurrentActor(), cancellationToken);
        TempData["Success"] = $"已将 {entries.Count} 件作品加入官网作品集。";
        return RedirectToAction(nameof(Index), new { artistId });
    }

    [HttpPost("Works/Reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderWorks(int artistId, [FromBody] int[] ids, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.ReorderWorksAsync(artistId, ids, CurrentActor(), cancellationToken);
            return Ok(new { success = true });
        }
        catch (DbException)
        {
            return Conflict(new { error = "后台数据库缺少 Works.Sort，请先执行艺术家官网升级脚本。" });
        }
    }

    [HttpPost("Exhibitions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveExhibitions(
        int artistId,
        int[] publicationIds,
        bool hideDefaultExhibitions,
        CancellationToken cancellationToken)
    {
        var overview = await _repository.GetOverviewAsync(artistId, cancellationToken);
        if (overview is null)
        {
            return NotFound();
        }

        if (!overview.ExhibitionLinksSupported || !overview.DefaultExhibitionFlagSupported)
        {
            TempData["Error"] = "后台数据库缺少 ArtistPublication 或 Artist.HideDefaultExhibitions，展讯数据未修改。请先执行 docs/sql/2026-10-26-artist-site.sql。";
            return RedirectToAction(nameof(Index), new { artistId });
        }

        try
        {
            if (!await _repository.SaveExhibitionLinksAsync(artistId, publicationIds, hideDefaultExhibitions, CurrentActor(), cancellationToken))
            {
                return NotFound();
            }

            TempData["Success"] = "展讯已保存。";
        }
        catch (DbException)
        {
            TempData["Error"] = "后台数据库结构尚未升级，展讯数据未修改。";
        }

        return RedirectToAction(nameof(Index), new { artistId });
    }

    private static IReadOnlyList<PublicationOption> LegacyPublicationOptions()
    {
        var options = new List<PublicationOption>();
        foreach (var id in Enumerable.Range(92001, 15).Where(id => id != 92004))
        {
            var fallback = LegacyPublicationGalleryCatalog.GetFallbackPublication(id)?.Publication;
            if (fallback is not null)
            {
                options.Add(new PublicationOption(id, fallback.TitleCn, fallback.StartDate, fallback.Location));
            }
        }

        return options;
    }

    private static int ExhibitionOrder(ArtistExhibitionLinks exhibitions, int publicationId)
    {
        for (var index = 0; index < exhibitions.Linked.Count; index++)
        {
            if (exhibitions.Linked[index].Id == publicationId)
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }
}
