using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>
/// Cookie-based sign-in for the staff/admin backend, replacing the legacy <c>StaffController.Login</c>
/// (<c>Session["Staff"]</c> + MD5). Password verification stays MD5-compatible via
/// <see cref="StaffPasswordHasher"/> so existing accounts log in unchanged.
/// </summary>
[Area("Admin")]
public sealed class AccountController : Controller
{
    private readonly IOptions<AdminWriteOptions> _adminWrite;

    public AccountController(IOptions<AdminWriteOptions> adminWrite)
    {
        _adminWrite = adminWrite;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAdminHome(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // The admin backend needs a writable DB (Staff lives there). When it is unconfigured (a fresh dev
        // box) or unreachable, show a clear message on the login page instead of a 500.
        if (!_adminWrite.Value.IsConfigured)
        {
            ModelState.AddModelError(string.Empty,
                "后台尚未配置可写数据库，无法登录。请在 appsettings.Local.json 配置 AdminWrite:EnableWrites=true 与 ConnectionStrings:SzaipaAdmin（指向本地可写副本）。");
            return View(model);
        }

        // Resolve the admin context lazily (not via constructor) so the login page still renders when the
        // admin write path is unconfigured; only an actual sign-in attempt touches the database.
        var db = HttpContext.RequestServices.GetRequiredService<SzaipaAdminContext>();
        Staff? staff;
        try
        {
            staff = await db.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StaffName == model.StaffName);
        }
        catch (DbException)
        {
            ModelState.AddModelError(string.Empty,
                "无法连接后台数据库，请检查 ConnectionStrings:SzaipaAdmin。");
            return View(model);
        }

        if (staff is null || !StaffPasswordHasher.Verify(model.Password ?? string.Empty, staff.Password))
        {
            ModelState.AddModelError(string.Empty, "账户不存在或密码错误！");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.StaffName ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToAdminHome(returnUrl);
    }

    [HttpPost]
    [Authorize(Policy = AdminAuthorization.StaffPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize(Policy = AdminAuthorization.StaffPolicy)]
    public IActionResult PasswordChange() => View(new PasswordChangeViewModel());

    [HttpPost]
    [Authorize(Policy = AdminAuthorization.StaffPolicy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasswordChange(PasswordChangeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var staffId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        var db = HttpContext.RequestServices.GetRequiredService<SzaipaAdminContext>();
        var staff = await db.Staff.FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff is null || !StaffPasswordHasher.Verify(model.CurrentPassword ?? string.Empty, staff.Password))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "原密码错误！");
            return View(model);
        }

        // Store the new password in the same lowercase-MD5 form legacy used, so it stays compatible.
        staff.Password = StaffPasswordHasher.Md5Hex(model.NewPassword ?? string.Empty);

        var recorder = HttpContext.RequestServices.GetRequiredService<IOperationRecorder>();
        var actor = new AdminActor(staff.Id, staff.StaffName ?? string.Empty);
        await recorder.RecordAsync(db, actor, "修改了登录密码。", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "密码已修改。";
        return RedirectToAction("Index", "Dashboard");
    }

    private IActionResult RedirectToAdminHome(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
}
