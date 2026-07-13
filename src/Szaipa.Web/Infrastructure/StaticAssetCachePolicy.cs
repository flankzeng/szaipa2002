namespace Szaipa.Web.Infrastructure;

public enum StaticAssetSource
{
    WebRoot,
    LegacyContent
}

/// <summary>
/// Selects browser cache directives for static assets without depending on an HTTP context.
/// Versioned assets from the application web root are immutable; legacy assets keep conservative,
/// extension-specific lifetimes because their URLs are not content-hashed.
/// </summary>
public static class StaticAssetCachePolicy
{
    public const string NoCache = "no-cache";
    public const string OneDay = "public,max-age=86400";
    public const string SevenDays = "public,max-age=604800";
    public const string ThirtyDays = "public,max-age=2592000";
    public const string OneYearImmutable = "public,max-age=31536000,immutable";

    public static string Select(
        bool isDevelopment,
        StaticAssetSource source,
        string? requestPath,
        bool hasContentVersion = false)
    {
        if (isDevelopment)
        {
            return NoCache;
        }

        if (source == StaticAssetSource.WebRoot)
        {
            return hasContentVersion ? OneYearImmutable : SevenDays;
        }

        var extension = Path.GetExtension(requestPath ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".avif" or ".svg" or ".ico" or ".bmp"
                => ThirtyDays,
            ".woff" or ".woff2" or ".ttf" or ".otf" or ".eot"
                => ThirtyDays,
            ".css" or ".js"
                => SevenDays,
            _ => OneDay
        };
    }
}
