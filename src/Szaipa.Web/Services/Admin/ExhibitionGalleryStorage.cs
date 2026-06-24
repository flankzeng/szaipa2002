using Microsoft.Extensions.Options;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Configuration;

namespace Szaipa.Web.Services.Admin;

/// <inheritdoc />
public sealed class ExhibitionGalleryStorage : IExhibitionGalleryStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private const long MaxBytes = 20L * 1024 * 1024;

    private readonly IOptions<LegacyAssetsOptions> _legacyAssets;

    public ExhibitionGalleryStorage(IOptions<LegacyAssetsOptions> legacyAssets)
    {
        _legacyAssets = legacyAssets;
    }

    public bool IsWritable
    {
        get
        {
            var root = _legacyAssets.Value.ContentRoot;
            return !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
        }
    }

    public async Task<GalleryImage?> AddUploadAsync(string folder, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > MaxBytes)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return null;
        }

        var folderPath = ResolveFolderPath(folder);
        if (folderPath is null)
        {
            return null;
        }

        await using var stream = file.OpenReadStream();
        var name = await ExhibitionGalleryFolder.AddUploadAsync(folderPath, stream, cancellationToken);
        return new GalleryImage(name, ImageUrl(folder, name));
    }

    public IReadOnlyList<GalleryImage> ListGallery(string folder)
    {
        var folderPath = ResolveFolderPath(folder);
        if (folderPath is null)
        {
            return Array.Empty<GalleryImage>();
        }

        return ExhibitionGalleryFolder.ListGallery(folderPath)
            .Select(name => new GalleryImage(name, ImageUrl(folder, name)))
            .ToList();
    }

    public int ApplyOrder(string folder, IReadOnlyList<string> orderedNames)
    {
        var folderPath = ResolveFolderPath(folder);
        return folderPath is null ? 0 : ExhibitionGalleryFolder.ApplyOrder(folderPath, orderedNames);
    }

    private string? ResolveFolderPath(string folder)
    {
        var root = _legacyAssets.Value.ContentRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        var safe = SanitizeFolder(folder);
        if (safe is null)
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(root, "images", safe));
        var imagesRoot = Path.GetFullPath(Path.Combine(root, "images"));
        return path.StartsWith(imagesRoot, StringComparison.Ordinal) ? path : null;
    }

    private static string ImageUrl(string folder, string name) => $"/Content/images/{SanitizeFolder(folder)}/{name}";

    private static string? SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var trimmed = folder.Trim().Trim('/', '\\');
        return trimmed.Contains("..") || trimmed.Contains('/') || trimmed.Contains('\\') || trimmed.Contains(':')
            ? null
            : trimmed;
    }
}
