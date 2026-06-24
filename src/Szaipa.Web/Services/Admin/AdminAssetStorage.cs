using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services.Admin;

/// <summary>
/// <see cref="IAdminAssetStorage"/> over the configured <see cref="LegacyAssetsOptions.ContentRoot"/>. The
/// admin machine points ContentRoot at a LOCAL writable copy of the legacy Content tree, so writes here
/// never touch production assets.
/// </summary>
public sealed class AdminAssetStorage : IAdminAssetStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    private const long MaxBytes = 20L * 1024 * 1024; // 20 MB, matching the legacy upload cap.

    private readonly IOptions<LegacyAssetsOptions> _legacyAssets;

    public AdminAssetStorage(IOptions<LegacyAssetsOptions> legacyAssets)
    {
        _legacyAssets = legacyAssets;
    }

    public async Task<AdminAssetSaveResult> SaveImageAsync(
        IFormFile file,
        string subfolder,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return AdminAssetSaveResult.Fail("未接收到文件。");
        }

        if (file.Length > MaxBytes)
        {
            return AdminAssetSaveResult.Fail("图片过大，请勿超过 20M。");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return AdminAssetSaveResult.Fail("图片格式不支持（仅 jpg/png/gif/bmp/webp）。");
        }

        var contentRoot = _legacyAssets.Value.ContentRoot;
        if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
        {
            return AdminAssetSaveResult.Fail(
                "未配置可写的内容目录（LegacyAssets:ContentRoot）。请指向本地可写的 Content 副本。");
        }

        var safeSubfolder = SanitizeSubfolder(subfolder);
        var targetDirectory = Path.Combine(contentRoot, safeSubfolder.Replace('/', Path.DirectorySeparatorChar));

        // Defense in depth: never let a crafted subfolder escape the content root.
        var rootFull = Path.GetFullPath(contentRoot);
        var targetFull = Path.GetFullPath(targetDirectory);
        if (!targetFull.StartsWith(rootFull, StringComparison.Ordinal))
        {
            return AdminAssetSaveResult.Fail("非法的目标目录。");
        }

        Directory.CreateDirectory(targetDirectory);

        // Unique GUID name: a saved record's images can never collide with or overwrite another record's.
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(targetDirectory, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        // Served by the /Content static-file mapping (Program.cs) over the same ContentRoot.
        var url = $"/Content/{safeSubfolder}/{fileName}";
        return AdminAssetSaveResult.Ok(url, fileName);
    }

    private static string SanitizeSubfolder(string subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
        {
            return "newsImg";
        }

        // Allow safe nested folders (e.g. "ArtImg/Artist/ArtNews") but reject traversal, drive letters,
        // and any segment with invalid file-name characters.
        var invalid = Path.GetInvalidFileNameChars();
        var segments = subfolder.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var clean = new List<string>();
        foreach (var raw in segments)
        {
            var segment = raw.Trim();
            if (segment == ".")
            {
                continue;
            }

            if (segment == ".." || segment.Contains(':') || segment.IndexOfAny(invalid) >= 0)
            {
                return "newsImg";
            }

            clean.Add(segment);
        }

        return clean.Count == 0 ? "newsImg" : string.Join('/', clean);
    }
}
