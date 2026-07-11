using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Data.Configuration;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;

namespace Szaipa.Web.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Operations")]
public sealed class OperationsController : Controller
{
    private const int PageSize = 15;
    private readonly AdminWriteOptions _adminWrite;

    public OperationsController(IOptions<AdminWriteOptions> adminWrite)
    {
        _adminWrite = adminWrite.Value;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (!_adminWrite.IsConfigured)
        {
            return View(new OperationHistoryViewModel
            {
                DataAvailable = false,
                UnavailableReason = "未配置后台数据库，操作记录暂不可用。"
            });
        }

        try
        {
            var repository = HttpContext.RequestServices.GetRequiredService<IDashboardAnalyticsRepository>();
            var history = await repository.GetOperationHistoryAsync(page, PageSize, cancellationToken);
            return View(new OperationHistoryViewModel { DataAvailable = true, History = history });
        }
        catch (DbException)
        {
            return View(new OperationHistoryViewModel
            {
                DataAvailable = false,
                UnavailableReason = "无法连接数据库，操作记录暂不可用。"
            });
        }
    }
}
