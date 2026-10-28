using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Web.Authorization;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services.Traffic;

namespace Szaipa.Web.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/SecurityMonitoring/Interactions")]
public sealed class TrafficInteractionsController : Controller
{
    private readonly ITrafficEventStore _store;
    private readonly IOptions<SecurityMonitoringOptions> _options;

    public TrafficInteractionsController(ITrafficEventStore store, IOptions<SecurityMonitoringOptions> options)
    {
        _store = store;
        _options = options;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int days = 7, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.RequestAuditEnabled) return Json(new { views = 0, clicks = 0, topPages = Array.Empty<TrafficTopPage>(), recent = Array.Empty<TrafficEvent>(), sourceTruncated = false });
        var summary = await _store.ReadSummaryAsync(Math.Clamp(days, 1, 30), Math.Clamp(limit, 1, 100), cancellationToken);
        return Json(summary);
    }
}
