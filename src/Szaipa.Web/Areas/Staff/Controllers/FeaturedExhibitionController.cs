using System.Security.Claims;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

[Area("Staff"), Authorize(Policy = AdminAuthorization.StaffPolicy), Route("Staff/FeaturedExhibition")]
public sealed class FeaturedExhibitionController(IFeaturedExhibitionAdminRepository repository) : Controller
{
    [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View(await repository.GetAllAsync(ct)); }
        catch (DbException exception) when (IsMissingFeaturedSchema(exception))
        {
            ViewData["SchemaWarning"] = "首页推荐表或字段尚未创建，请先执行展览自动化 SQL。";
            return View(Array.Empty<FeaturedExhibition>());
        }
    }
    [HttpGet("Create")] public IActionResult Create() => View("Form", new FeaturedExhibition { IsVisible = true });
    [HttpPost("Create"), ValidateAntiForgeryToken] public async Task<IActionResult> Create(FeaturedExhibition model, CancellationToken ct) { if (!ModelState.IsValid) return View("Form", model); try { await repository.CreateAsync(model, Actor(), ct); TempData["Success"]="首页推荐已添加。"; return RedirectToAction(nameof(Index)); } catch (DbException exception) when (IsMissingFeaturedSchema(exception)) { ModelState.AddModelError(string.Empty,"首页推荐表结构尚未创建，请先执行展览自动化 SQL。"); return View("Form", model); } }
    [HttpGet("Edit/{id:int}")] public async Task<IActionResult> Edit(int id,CancellationToken ct) { try { var row=await repository.GetByIdAsync(id,ct); return row is null?NotFound():View("Form",row); } catch (DbException exception) when (IsMissingFeaturedSchema(exception)) { TempData["Error"]="首页推荐表结构尚未创建，请先执行展览自动化 SQL。"; return RedirectToAction(nameof(Index)); } }
    [HttpPost("Edit/{id:int}"), ValidateAntiForgeryToken] public async Task<IActionResult> Edit(int id,FeaturedExhibition model,CancellationToken ct) { model.Id=id; if(!ModelState.IsValid)return View("Form",model); try { if(!await repository.UpdateAsync(model,Actor(),ct))return NotFound(); TempData["Success"]="首页推荐已保存。"; return RedirectToAction(nameof(Index)); } catch (DbException exception) when (IsMissingFeaturedSchema(exception)) { ModelState.AddModelError(string.Empty,"首页推荐表结构尚未创建，请先执行展览自动化 SQL。"); return View("Form",model); } }
    [HttpPost("Delete/{id:int}"), ValidateAntiForgeryToken] public async Task<IActionResult> Delete(int id,CancellationToken ct) { await repository.DeleteAsync(id,Actor(),ct); return RedirectToAction(nameof(Index)); }
    [HttpPost("Move/{id:int}"), ValidateAntiForgeryToken] public async Task<IActionResult> Move(int id,int direction,CancellationToken ct) { await repository.MoveAsync(id,direction,Actor(),ct); return RedirectToAction(nameof(Index)); }
    private AdminActor Actor()=>new(int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0,User.Identity?.Name??string.Empty);
    private static bool IsMissingFeaturedSchema(DbException exception) =>
        SchemaCompatibility.IsMissingTable(exception, "FeaturedExhibition") || SchemaCompatibility.IsMissingColumn(exception);
}
