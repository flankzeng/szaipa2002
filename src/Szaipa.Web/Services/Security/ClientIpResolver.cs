using Microsoft.AspNetCore.Http;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// Resolves the client IP for auditing. Trusts the left-most non-empty X-Forwarded-For hop when present
/// (set by the fronting load balancer / reverse proxy), else the direct connection address.
/// </summary>
public static class ClientIpResolver
{
    public static string Resolve(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // The chain is "client, proxy1, proxy2" — the first hop is the original client.
            var firstHop = forwardedFor.Split(',', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstHop))
            {
                return firstHop;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }
}
