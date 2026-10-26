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

    /// <summary>
    /// 裁切组件用：把「未裁切原图」另存到 <c>{subfolder}/original/{裁切图同名主干}.{原图扩展名}</c>，
    /// 以便以后重新裁切。返回原图的公开 URL；格式/大小/目录不合法或无可写目录时返回 null（不抛异常）。
    /// </summary>
    Task<string?> SaveOriginalAsync(
        IFormFile original,
        string subfolder,
        string croppedFileName,
        CancellationToken cancellationToken);

    /// <summary>重新裁切后产生了新的裁切图：把旧裁切图的原图复制一份给新文件名（原图只存一份来源，不重复上传）。</summary>
    Task<string?> CloneOriginalAsync(
        string subfolder,
        string previousFileName,
        string croppedFileName,
        CancellationToken cancellationToken);

    /// <summary>查找某个已保存文件的原图 URL；没有（老数据，从未经过裁切）返回 null。</summary>
    string? FindOriginalUrl(string subfolder, string fileName);
}
