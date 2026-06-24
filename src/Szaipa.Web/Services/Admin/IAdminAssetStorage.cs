namespace Szaipa.Web.Services.Admin;

/// <summary>Outcome of an admin image upload.</summary>
public sealed record AdminAssetSaveResult(bool Success, string? Url, string? FileName, string? Error)
{
    public static AdminAssetSaveResult Ok(string url, string fileName) => new(true, url, fileName, null);

    public static AdminAssetSaveResult Fail(string error) => new(false, null, null, error);
}

/// <summary>
/// Writes admin-uploaded images into the legacy Content tree (served read-only at <c>/Content</c>) and
/// returns their public URL. Unlike the legacy temp-then-move-and-substring-delete flow, files are written
/// once under a unique GUID name directly into their final folder, so a saved record's images can never be
/// clobbered by another record and there is no shared mutable upload state to leak between requests.
/// </summary>
public interface IAdminAssetStorage
{
    /// <summary>
    /// Saves <paramref name="file"/> into <c>{ContentRoot}/{subfolder}</c> under a unique GUID file name and
    /// returns the public <c>/Content/{subfolder}/{name}</c> URL. Validates extension and size; returns a
    /// failure result (never throws) for bad input or when no writable content root is configured.
    /// </summary>
    Task<AdminAssetSaveResult> SaveImageAsync(IFormFile file, string subfolder, CancellationToken cancellationToken);
}
