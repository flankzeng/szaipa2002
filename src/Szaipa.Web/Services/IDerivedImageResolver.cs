namespace Szaipa.Web.Services;

/// <summary>
/// A high-quality, application-owned image variant with its legacy source kept as the browser fallback.
/// </summary>
public sealed record DerivedImageSource(string OriginalUrl, string? AvifUrl);

/// <summary>
/// Resolves an allowlisted legacy <c>/Content</c> image to an optional, application-owned AVIF variant.
/// Invalid or unsafe source URLs are rejected; valid unlisted URLs remain usable as legacy fallbacks.
/// </summary>
public interface IDerivedImageResolver
{
    DerivedImageSource? Resolve(string? originalUrl);
}
