namespace Szaipa.Web.Services;

/// <summary>
/// Selects an existing, bandwidth-saving preview for an image served from the legacy
/// <c>/Content</c> tree. Inputs that cannot be resolved safely are returned unchanged.
/// </summary>
public interface ILegacyImagePreviewResolver
{
    string Resolve(string originalUrl);
}
