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

    /// <summary>原图子目录名；与裁切图同主干文件名，扩展名取原图自己的。</summary>
    private const string OriginalFolderName = "original";

    public async Task<string?> SaveOriginalAsync(
        IFormFile original,
        string subfolder,
        string croppedFileName,
        CancellationToken cancellationToken)
    {
        if (original is null || original.Length == 0 || original.Length > MaxBytes)
        {
            return null;
        }

        var extension = Path.GetExtension(original.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return null;
        }

        var stem = SafeStem(croppedFileName);
        if (stem is null || !TryResolveOriginalDirectory(subfolder, out var directory, out var safeSubfolder))
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        var fileName = stem + extension.ToLowerInvariant();
        await using (var stream = new FileStream(
            Path.Combine(directory, fileName), FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await original.CopyToAsync(stream, cancellationToken);
        }

        return $"/Content/{safeSubfolder}/{OriginalFolderName}/{fileName}";
    }

    public async Task<string?> CloneOriginalAsync(
        string subfolder,
        string previousFileName,
        string croppedFileName,
        CancellationToken cancellationToken)
    {
        var newStem = SafeStem(croppedFileName);
        if (newStem is null || !TryResolveOriginalDirectory(subfolder, out var directory, out var safeSubfolder))
        {
            return null;
        }

        var existing = FindOriginalFile(directory, previousFileName);
        if (existing is null)
        {
            return null;
        }

        var fileName = newStem + Path.GetExtension(existing).ToLowerInvariant();
        await using (var source = new FileStream(existing, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var target = new FileStream(
            Path.Combine(directory, fileName), FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return $"/Content/{safeSubfolder}/{OriginalFolderName}/{fileName}";
    }

    public string? FindOriginalUrl(string subfolder, string fileName)
    {
        if (!TryResolveOriginalDirectory(subfolder, out var directory, out var safeSubfolder))
        {
            return null;
        }

        var existing = FindOriginalFile(directory, fileName);
        return existing is null
            ? null
            : $"/Content/{safeSubfolder}/{OriginalFolderName}/{Path.GetFileName(existing)}";
    }

    private static string? FindOriginalFile(string directory, string fileName)
    {
        var stem = SafeStem(fileName);
        if (stem is null || !Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, stem + ".*")
            .FirstOrDefault(path => AllowedExtensions.Contains(Path.GetExtension(path)));
    }

    /// <summary>取文件名主干；含路径分隔符/越级/空的一律拒绝，防止借文件名逃出目录。</summary>
    private static string? SafeStem(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = fileName.Trim();
        if (name != Path.GetFileName(name) || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(stem) || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? null
            : stem;
    }

    private bool TryResolveOriginalDirectory(string subfolder, out string directory, out string safeSubfolder)
    {
        directory = string.Empty;
        safeSubfolder = SanitizeSubfolder(subfolder);
        var contentRoot = _legacyAssets.Value.ContentRoot;
        if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(
            contentRoot, safeSubfolder.Replace('/', Path.DirectorySeparatorChar), OriginalFolderName));
        if (!candidate.StartsWith(Path.GetFullPath(contentRoot), StringComparison.Ordinal))
        {
            return false;
        }

        directory = candidate;
        return true;
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
