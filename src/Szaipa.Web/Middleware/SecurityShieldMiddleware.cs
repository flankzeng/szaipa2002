using Microsoft.AspNetCore.Http;
using Szaipa.Web.Services.Security;

namespace Szaipa.Web.Middleware;

/// <summary>
/// 放在管道最前面（2026-10-28）：
/// <list type="number">
/// <item>给所有响应加通用安全头（禁止 MIME 猜测、只允许同源嵌套、限制 Referer 外泄、关闭用不到的浏览器能力）；</item>
/// <item>已封禁的扫描 IP 直接 403；</item>
/// <item>探测路径（.php、wp-、.env、.git …）直接 404 并计数，达到阈值封禁，见 <see cref="ScannerShield"/>。</item>
/// </list>
/// X-Frame-Options 取 SAMEORIGIN：后台「现场编辑」用同源 iframe 承载前台页面。
/// </summary>
public sealed class SecurityShieldMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ScannerShield _shield;

    public SecurityShieldMiddleware(RequestDelegate next, ScannerShield shield)
    {
        _next = next;
        _shield = shield;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
            return Task.CompletedTask;
        }, context);

        var ip = ClientIpResolver.Resolve(context);
        if (_shield.IsBanned(ip))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        if (ScannerShield.IsScannerPath(context.Request.Path.Value))
        {
            _shield.RecordScan(ip);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
