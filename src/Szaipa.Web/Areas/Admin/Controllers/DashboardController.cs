using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>
/// Landing page of the migrated staff backend (legacy <c>StaffController.Index</c> "后台综合"). Phase 0
/// ships the authenticated shell; visit tallies and the operation-record feed are wired in later phases.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
