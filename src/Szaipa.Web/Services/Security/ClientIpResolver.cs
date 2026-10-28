using Microsoft.AspNetCore.Http;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// Uses the connection address for auditing. IIS integration / configured trusted forwarding middleware
/// may normalize this address before auditing; raw client-supplied forwarding headers are not identity.
/// </summary>
public static class ClientIpResolver
{
    public static string Resolve(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }
}
