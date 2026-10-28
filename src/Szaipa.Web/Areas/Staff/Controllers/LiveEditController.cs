using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.LiveEdit;
using Szaipa.Web.Authorization;
using Szaipa.Web.LiveEdit;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// 现场编辑（T6）：后台用 iframe 承载「微缩版」前台（手机/平板/桌面），前台页面在编辑态下可直接点文字修改。
/// 进入本页时写入会话 cookie（仅已登录后台的请求才会让前台进入编辑态），退出时清除。
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/LiveEdit")]
public sealed class LiveEditController : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? path)
    {
        Response.Cookies.Append(LiveEditState.CookieName, "1", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/",
            IsEssential = true
        });
        ViewData["StartPath"] = IsLocalPublicPath(path) ? path : "/";
        return View();
    }

    [HttpGet("Exit")]
    public IActionResult Exit()
    {
        Response.Cookies.Delete(LiveEditState.CookieName, new CookieOptions { Path = "/" });
        return RedirectToAction("Index", "Dashboard");
    }

    public sealed class SaveRequest
    {
        public string? Kind { get; set; }
        public string? Key { get; set; }
        public string? Entity { get; set; }
        public int Id { get; set; }
        public string? Field { get; set; }
        public string? TextKind { get; set; }
        public string? Value { get; set; }
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveRequest request, CancellationToken cancellationToken)
    {
        // 后台写库未配置（如本地只读调试）时不 500，给出可读提示。
        var writeOptions = HttpContext.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<AdminWriteOptions>>()?.Value;
        if (writeOptions is null || !writeOptions.IsConfigured)
        {
            return Json(new { success = false, error = "后台写入未启用（AdminWrite），当前环境只能浏览。" });
        }

        var service = HttpContext.RequestServices.GetRequiredService<LiveEditService>();
        var actor = CurrentActor();
        var result = string.Equals(request.Kind, "site", StringComparison.Ordinal)
            ? await service.SaveSiteTextAsync(request.Key, request.Value,
                string.Equals(request.TextKind, "lines", StringComparison.Ordinal) ? LiveEditKind.Lines : LiveEditKind.Text,
                actor, cancellationToken)
            : await service.SaveFieldAsync(request.Entity, request.Id, request.Field, request.Value, actor, cancellationToken);
        return Json(new { success = result.Success, value = result.Value, error = result.Error });
    }

    // ---- 首页「核心成员 / 顾问团队」：拖拽排序、点击新增（数据仍是后台「组织架构」OrgMember） ----

    public sealed class ReorderRequest
    {
        public string? Entity { get; set; }
        public List<int>? Ids { get; set; }
    }

    /// <summary>拖拽松手后提交整组的新顺序（页面上看到的 id 顺序）。目前只开放 OrgMember。</summary>
    [HttpPost("Reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken cancellationToken)
    {
        if (WriteDisabled() is { } disabled)
        {
            return disabled;
        }

        if (!string.Equals(request.Entity, nameof(Szaipa.Data.Contexts.Szaipa.OrgMember), StringComparison.Ordinal)
            || request.Ids is not { Count: > 0 and <= 200 })
        {
            return Json(new { success = false, error = "不支持的排序请求。" });
        }

        var repository = HttpContext.RequestServices.GetRequiredService<IOrgMemberAdminRepository>();
        var done = await repository.ReorderAsync(request.Ids, CurrentActor(), cancellationToken);
        return Json(new { success = done, error = done ? null : "成员不存在或不在同一分组，请刷新后重试。" });
    }

    /// <summary>新增成员弹窗的下拉数据：可关联的艺术家（带头像）与职务 / 头衔建议。</summary>
    [HttpGet("OrgMemberOptions")]
    public async Task<IActionResult> OrgMemberOptions(CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IOrgMemberAdminRepository>();
        var variants = HttpContext.RequestServices.GetRequiredService<Szaipa.Web.Services.IImageVariants>();
        var artists = await repository.GetArtistOptionsAsync(cancellationToken);
        return Json(new
        {
            artists = artists.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                avatar = string.IsNullOrWhiteSpace(a.Path) ? null : variants.Url($"/Content/ArtImg/Artist/{a.Path}", 480)
            }),
            roles = Szaipa.Data.Contexts.Szaipa.OrgRoles.Roles,
            titles = Szaipa.Data.Contexts.Szaipa.OrgRoles.Titles
        });
    }

    public sealed class CreateMemberRequest
    {
        public string? Group { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Title { get; set; }
        public int? ArtistId { get; set; }
        public string? AvatarPath { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }
    }

    /// <summary>在首页直接新增一位成员，排在该分组最后（之后可拖拽调整）。</summary>
    [HttpPost("OrgMember/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMember([FromBody] CreateMemberRequest request, CancellationToken cancellationToken)
    {
        if (WriteDisabled() is { } disabled)
        {
            return disabled;
        }

        var repository = HttpContext.RequestServices.GetRequiredService<IOrgMemberAdminRepository>();
        if (request.Group is null || !Szaipa.Data.Contexts.Szaipa.OrgGroups.All.Contains(request.Group))
        {
            return Json(new { success = false, error = "未知的分组。" });
        }

        string? artistName = null;
        if (request.ArtistId is { } artistId)
        {
            artistName = (await repository.GetArtistOptionsAsync(cancellationToken)).FirstOrDefault(a => a.Id == artistId)?.Name;
            if (artistName is null)
            {
                return Json(new { success = false, error = "所选艺术家不存在。" });
            }
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? artistName : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
        {
            return Json(new { success = false, error = "请填写姓名（50 字以内），或选择一位艺术家。" });
        }

        // 头像只接受站内 /Content 路径（上传接口返回的就是这种），防止写入外链。
        var avatar = string.IsNullOrWhiteSpace(request.AvatarPath) ? null : request.AvatarPath.Trim();
        if (avatar is not null && (!avatar.StartsWith("/Content/", StringComparison.OrdinalIgnoreCase) || avatar.Contains("..", StringComparison.Ordinal)))
        {
            return Json(new { success = false, error = "头像地址无效，请重新上传。" });
        }

        var id = await repository.CreateAsync(new Szaipa.Data.Contexts.Szaipa.OrgMember
        {
            Name = name,
            Role = request.Role,
            Title = request.Title,
            GroupName = request.Group,
            ArtistId = request.ArtistId,
            AvatarPath = avatar,
            ImageWidth = request.ImageWidth is > 0 and < 20000 ? request.ImageWidth : null,
            ImageHeight = request.ImageHeight is > 0 and < 20000 ? request.ImageHeight : null,
            IsVisible = true
        }, CurrentActor(), cancellationToken);
        return Json(new { success = true, id });
    }

    private IActionResult? WriteDisabled()
    {
        var writeOptions = HttpContext.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<AdminWriteOptions>>()?.Value;
        return writeOptions is null || !writeOptions.IsConfigured
            ? Json(new { success = false, error = "后台写入未启用（AdminWrite），当前环境只能浏览。" })
            : null;
    }

    /// <summary>只允许站内前台路径（防止 iframe 被带去外站或后台页）。</summary>
    private static bool IsLocalPublicPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith('/')
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.StartsWith("/Staff", StringComparison.OrdinalIgnoreCase)
        && !path.Contains('\\');

    private AdminActor CurrentActor()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        return new AdminActor(id, User.Identity?.Name ?? string.Empty);
    }
}
