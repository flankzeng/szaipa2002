using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services.Security;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// Security-monitoring dashboard for the staff backend. Surfaces three data streams:
/// (1) server login attempts &amp; abnormal access from C:\Security (blocked_ips.csv, whitelist.txt,
/// alert_errors.log, security_snapshot.json), (2) website abnormal traffic from the rolling request audit,
/// (3) staff-backend login attempts. Actions (unblock / whitelist edit) only enqueue request files for the
/// privileged SYSTEM task — the web app never runs netsh or edits the server files directly.
/// </summary>
[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/SecurityMonitoring")]
public sealed class SecurityMonitoringController : Controller
{
    private readonly IServerSecurityRepository _repository;
    private readonly ISecurityAuditReader _auditReader;
    private readonly ISecurityActionQueue _actionQueue;
    private readonly SecurityMonitoringPaths _paths;
    private readonly IOptions<SecurityMonitoringOptions> _options;

    public SecurityMonitoringController(
        IServerSecurityRepository repository,
        ISecurityAuditReader auditReader,
        ISecurityActionQueue actionQueue,
        SecurityMonitoringPaths paths,
        IOptions<SecurityMonitoringOptions> options)
    {
        _repository = repository;
        _auditReader = auditReader;
        _actionQueue = actionQueue;
        _paths = paths;
        _options = options;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        var configured = _repository.IsConfigured;
        return View(new SecurityMonitoringViewModel
        {
            DataAvailable = configured,
            UnavailableReason = configured
                ? null
                : $"服务器安全目录未就绪（当前：{_paths.ServerSecurityRoot}）。请配置 SecurityMonitoring:ServerSecurityRoot 并部署 scripts/security-snapshot.ps1 计划任务。",
            RequestAuditEnabled = _options.Value.RequestAuditEnabled,
            ServerSecurityRoot = _paths.ServerSecurityRoot,
            SnapshotGeneratedAtUtc = _repository.ReadSnapshot()?.GeneratedAtUtc,
        });
    }

    [HttpGet("Kpis")]
    public IActionResult Kpis()
    {
        var snapshot = Safe(() => _repository.ReadSnapshot());
        var blocked = Safe(() => _repository.ReadBlockedIps());
        var whitelist = Safe(() => _repository.ReadWhitelist());
        var alerts = Safe(() => _repository.ReadAlertErrors(50));
        var since = DateTimeOffset.Now.AddHours(-24);
        var requests = Safe(() => _auditReader.ReadRequests(since, 200_000));
        var logins = Safe(() => _auditReader.ReadLogins(500));

        return Json(new
        {
            snapshot,
            blockedCount = blocked?.Count ?? 0,
            whitelistCount = whitelist?.Count ?? 0,
            alertCount = alerts?.Count ?? 0,
            requestAuditEnabled = _options.Value.RequestAuditEnabled,
            auditRequestsLast24h = requests?.Count ?? 0,
            auditLoginAttemptsLast24h = logins?.Count ?? 0,
            auditLoginFailsLast24h = logins?.Count(l => l.Success == false) ?? 0,
        });
    }

    [HttpGet("BlockedIps")]
    public IActionResult BlockedIps()
    {
        var blocked = Safe(() => _repository.ReadBlockedIps());
        return Json(new
        {
            items = (blocked ?? []).Select(b => new { b.Ip, b.BlockedAtUtc, b.RemainingSeconds }),
        });
    }

    [HttpGet("Whitelist")]
    public IActionResult Whitelist()
    {
        var whitelist = Safe(() => _repository.ReadWhitelist());
        return Json(new { items = whitelist ?? Array.Empty<string>() });
    }

    [HttpGet("Alerts")]
    public IActionResult Alerts()
    {
        var lines = Safe(() => _repository.ReadAlertErrors(100));
        return Json(new
        {
            items = lines?.Select(ParseAlertLine) ?? Array.Empty<object>(),
        });
    }

    [HttpGet("Snapshot")]
    public IActionResult Snapshot()
    {
        return Json(Safe(() => _repository.ReadSnapshot()) ?? new SecuritySnapshot());
    }

    [HttpGet("WebTraffic")]
    public IActionResult WebTraffic(int days = 14)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTimeOffset.Now.AddDays(-(days - 1)).Date;
        var lines = Safe(() => _auditReader.ReadRequests(since, 200_000)) ?? Array.Empty<RequestLogLine>();

        var byDay = new Dictionary<DateTime, (long Requests, long Errors)>();
        for (var d = 0; d < days; d++)
        {
            byDay[DateTimeOffset.Now.AddDays(-d).Date] = (0, 0);
        }

        foreach (var line in lines)
        {
            if (line.Timestamp is not { } timestamp)
            {
                continue;
            }

            var key = timestamp.Date;
            if (!byDay.TryGetValue(key, out var current))
            {
                continue;
            }

            var errors = line.Status is { } status && status >= 400 ? 1 : 0;
            byDay[key] = (current.Requests + 1, current.Errors + errors);
        }

        var ordered = byDay.OrderBy(kv => kv.Key).ToList();
        return Json(new
        {
            categories = ordered.Select(kv => kv.Key.ToString("MM-dd")),
            requests = ordered.Select(kv => kv.Value.Requests),
            errors = ordered.Select(kv => kv.Value.Errors),
        });
    }

    [HttpGet("TopSources")]
    public IActionResult TopSources(int days = 7, int limit = 10)
    {
        days = Math.Clamp(days, 1, 30);
        limit = Math.Clamp(limit, 1, 50);
        var since = DateTimeOffset.Now.AddDays(-(days - 1)).Date;
        var lines = Safe(() => _auditReader.ReadRequests(since, 200_000)) ?? Array.Empty<RequestLogLine>();

        var grouped = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Ip))
            .GroupBy(l => l.Ip!)
            .Select(g => new { ip = g.Key, count = g.LongCount() })
            .OrderByDescending(g => g.count)
            .Take(limit);

        return Json(new { items = grouped });
    }

    [HttpGet("StaffLogins")]
    public IActionResult StaffLogins(int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 200);
        var lines = Safe(() => _auditReader.ReadLogins(limit)) ?? Array.Empty<LoginLogLine>();
        return Json(new
        {
            items = lines.Select(l => new { l.Timestamp, l.Username, l.Ip, l.Success }),
        });
    }

    [HttpGet("Actions")]
    public IActionResult Actions()
    {
        var actions = Safe(() => _repository.ReadQueueActions());
        return Json(new { items = actions ?? Array.Empty<QueueAction>() });
    }

    [HttpPost("Unblock")]
    [ValidateAntiForgeryToken]
    public IActionResult Unblock([FromBody] SecurityIpRequest request)
    {
        var ip = SecurityIpPolicy.NormalizeIp(request?.Ip);
        if (ip is null)
        {
            return Json(new { ok = false, message = "IP 地址无效，仅支持 IPv4。" });
        }

        var ok = _actionQueue.WriteUnblock(ip, User.Identity?.Name ?? string.Empty, DateTimeOffset.Now);
        return Json(ok
            ? new { ok = true, message = $"已提交解封 {ip}，等待服务器计划任务处理。" }
            : new { ok = false, message = "队列写入失败，请检查目录权限。" });
    }

    [HttpPost("WhitelistAdd")]
    [ValidateAntiForgeryToken]
    public IActionResult WhitelistAdd([FromBody] SecurityIpRequest request)
    {
        var ip = SecurityIpPolicy.NormalizeIp(request?.Ip);
        if (ip is null)
        {
            return Json(new { ok = false, message = "IP 地址无效，仅支持 IPv4。" });
        }

        var ok = _actionQueue.WriteWhitelistEdit(ip, add: true, User.Identity?.Name ?? string.Empty, DateTimeOffset.Now);
        return Json(ok
            ? new { ok = true, message = $"已提交白名单添加 {ip}，等待服务器计划任务处理。" }
            : new { ok = false, message = "队列写入失败，请检查目录权限。" });
    }

    [HttpPost("WhitelistRemove")]
    [ValidateAntiForgeryToken]
    public IActionResult WhitelistRemove([FromBody] SecurityIpRequest request)
    {
        var ip = SecurityIpPolicy.NormalizeIp(request?.Ip);
        if (ip is null)
        {
            return Json(new { ok = false, message = "IP 地址无效，仅支持 IPv4。" });
        }

        var ok = _actionQueue.WriteWhitelistEdit(ip, add: false, User.Identity?.Name ?? string.Empty, DateTimeOffset.Now);
        return Json(ok
            ? new { ok = true, message = $"已提交白名单移除 {ip}，等待服务器计划任务处理。" }
            : new { ok = false, message = "队列写入失败，请检查目录权限。" });
    }

    private static object ParseAlertLine(string line)
    {
        // alert_errors.log lines are free-form from the PowerShell task. Best-effort: split a leading
        // ISO-looking timestamp off the message if present.
        var trimmed = line.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex > 10 && DateTimeOffset.TryParse(trimmed[..spaceIndex], out var at))
        {
            return new { line = trimmed, at, level = "Error", message = trimmed[(spaceIndex + 1)..] };
        }

        return new { line = trimmed, at = (DateTimeOffset?)null, level = "Error", message = trimmed };
    }

    // File sources never throw for missing data — but a permission or IO hiccup must degrade to empty
    // JSON, never a 500 (the file-system analogue of the existing catch (DbException) idiom).
    private static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }

    public sealed class SecurityIpRequest
    {
        public string? Ip { get; set; }
    }
}
