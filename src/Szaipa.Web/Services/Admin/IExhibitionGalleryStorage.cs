namespace Szaipa.Web.Services.Admin;

public sealed record GalleryImage(string Name, string Url);

/// <summary>
/// Manages an exhibition's gallery images under <c>{ContentRoot}/images/{folder}</c>, served at
/// <c>/Content/images/{folder}/{n}.jpg</c>. Wraps the testable folder logic with IFormFile handling and
/// content-root resolution.
/// </summary>
public interface IExhibitionGalleryStorage
{
    bool IsWritable { get; }

    Task<GalleryImage?> AddUploadAsync(string folder, IFormFile file, CancellationToken cancellationToken);

    IReadOnlyList<GalleryImage> ListGallery(string folder);

    /// <summary>Renumbers the gallery to match <paramref name="orderedNames"/>; returns the new image count.</summary>
    int ApplyOrder(string folder, IReadOnlyList<string> orderedNames);
}
