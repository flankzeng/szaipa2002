using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Admin.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>
/// Landing page of the migrated staff backend (legacy <c>StaffController.Index</c> "后台综合"): KPI tiles +
/// operation-record feed rendered server-side, plus AJAX JSON endpoints feeding the ECharts visualisations
/// (daily-visit line, content-access doughnut, month/year province→city sunbursts). The analytics repository
/// is resolved lazily and guarded so the page still renders when the admin write DB is unconfigured (dev) or
/// temporarily unreachable, rather than 500-ing.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Dashboard")]
public sealed class DashboardController : Controller
{
    private const int RecentDays = 7;
    private const int DailyTrendDays = 30;

    private readonly IOptions<AdminWriteOptions> _adminWrite;

    public DashboardController(IOptions<AdminWriteOptions> adminWrite)
    {
        _adminWrite = adminWrite;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    [HttpGet("/Admin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_adminWrite.Value.IsConfigured)
        {
            return View(new DashboardViewModel
            {
                DataAvailable = false,
                UnavailableReason = "未配置可写数据库（AdminWrite:EnableWrites + ConnectionStrings:SzaipaAdmin），访问统计暂不可用。"
            });
        }

        try
        {
            var repository = Repository();
            var kpi = await repository.GetKpiAsync(RecentDays, cancellationToken);
            var operations = await repository.GetRecentOperationsAsync(RecentDays, cancellationToken);
            return View(new DashboardViewModel { DataAvailable = true, Kpi = kpi, Operations = operations });
        }
        catch (DbException)
        {
            return View(new DashboardViewModel
            {
                DataAvailable = false,
                UnavailableReason = "无法连接数据库，访问统计暂不可用。请检查 ConnectionStrings:SzaipaAdmin。"
            });
        }
    }

    [HttpGet("DailyVisits")]
    public async Task<IActionResult> DailyVisits(int days = DailyTrendDays, CancellationToken cancellationToken = default)
    {
        if (!_adminWrite.Value.IsConfigured)
        {
            return Json(new { categories = Array.Empty<string>(), data = Array.Empty<int>() });
        }

        var points = await Repository().GetDailyVisitsAsync(days, cancellationToken);
        return Json(new
        {
            categories = points.Select(p => p.Date),
            data = points.Select(p => p.Count)
        });
    }

    [HttpGet("ContentAccess")]
    public async Task<IActionResult> ContentAccess(CancellationToken cancellationToken)
    {
        if (!_adminWrite.Value.IsConfigured)
        {
            return Json(new { items = Array.Empty<object>() });
        }

        var slices = await Repository().GetContentAccessAsync(cancellationToken);
        return Json(new { items = slices.Select(s => new { name = s.Name, value = s.Value }) });
    }

    [HttpGet("Geo")]
    public async Task<IActionResult> Geo(string range = "month", CancellationToken cancellationToken = default)
    {
        if (!_adminWrite.Value.IsConfigured)
        {
            return Json(new { data = Array.Empty<object>() });
        }

        var today = DateTime.Today;
        var since = string.Equals(range, "year", StringComparison.OrdinalIgnoreCase)
            ? new DateTime(today.Year, 1, 1)
            : new DateTime(today.Year, today.Month, 1);

        var provinces = await Repository().GetGeoDistributionAsync(since, cancellationToken);
        return Json(new
        {
            data = provinces.Select(p => new
            {
                name = p.Name,
                value = p.Value,
                children = p.Children.Select(c => new { name = c.Name, value = c.Value })
            })
        });
    }

    // Resolved lazily (not via constructor) so the controller is constructable — and the page renders — even
    // when the admin write context is unconfigured; only an actual analytics call touches the database.
    private IDashboardAnalyticsRepository Repository() =>
        HttpContext.RequestServices.GetRequiredService<IDashboardAnalyticsRepository>();
}
