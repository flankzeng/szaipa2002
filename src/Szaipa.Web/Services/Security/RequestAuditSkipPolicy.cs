namespace Szaipa.Web.Services.Security;

/// <summary>
/// Decides which request paths are excluded from the request audit. Kept pure (no HttpContext) so it can
/// be unit-tested directly. Static assets are excluded because the middleware already runs after the static
/// file middleware; this additionally covers extension-based misses and the /Content legacy tree.
/// </summary>
public static class RequestAuditSkipPolicy
{
    private static readonly string[] StaticExtensions =
    {
        ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".webp", ".avif",
        ".woff", ".woff2", ".ttf", ".eot", ".map", ".json",
    };

    private static readonly string[] StaticPathPrefixes =
    {
        "/content/", "/css/", "/js/", "/lib/", "/admin/", "/images/", "/fonts/", "/media/",
    };

    public static bool ShouldSkip(string? path, IReadOnlyList<string> skipPrefixes)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        foreach (var prefix in skipPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var lower = path.ToLowerInvariant();
        foreach (var prefix in StaticPathPrefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var lastDot = lower.LastIndexOf('.');
        if (lastDot > 0 && lastDot < lower.Length - 1)
        {
            var extension = lower.Substring(lastDot);
            foreach (var staticExtension in StaticExtensions)
            {
                if (string.Equals(extension, staticExtension, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
