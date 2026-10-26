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
