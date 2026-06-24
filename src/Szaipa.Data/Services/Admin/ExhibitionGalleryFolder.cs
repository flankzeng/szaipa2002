namespace Szaipa.Data.Services.Admin;

/// <summary>
/// Manages an exhibition's gallery image folder on disk under the legacy numbering convention: gallery
/// images are <c>10001.jpg, 10002.jpg, …</c> (contiguous, so the frontend's <c>for (i = 10001..10001+MaxImg)</c>
/// loop renders them all); <c>10000.jpg</c> is the cover and is left untouched. New uploads are staged under
/// an <c>__u_*</c> prefix and only become numbered when an order is applied. All operations are pure
/// folder/file IO so they can be unit-tested against a temp directory.
/// </summary>
public static class ExhibitionGalleryFolder
{
    public const int FirstGalleryNumber = 10001;
    public const int CoverNumber = 10000;
    private const string UploadPrefix = "__u_";

    /// <summary>Writes a staged upload (not yet numbered) and returns its file name.</summary>
    public static async Task<string> AddUploadAsync(string folderPath, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folderPath);
        var name = $"{UploadPrefix}{Guid.NewGuid():N}.jpg";
        var full = Path.Combine(folderPath, name);
        await using var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(stream, cancellationToken);
        return name;
    }

    /// <summary>Existing gallery image file names (10001.jpg…) in ascending numeric order.</summary>
    public static IReadOnlyList<string> ListGallery(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(folderPath)
            .Select(Path.GetFileName)
            .Where(name => name is not null && GalleryNumber(name) >= FirstGalleryNumber)
            .OrderBy(name => GalleryNumber(name!))
            .Select(name => name!)
            .ToList();
    }

    /// <summary>
    /// Rewrites the gallery so the files referenced by <paramref name="orderedNames"/> become 10001.jpg…
    /// in that exact order; any other gallery/upload file is deleted; the cover (10000) is preserved.
    /// Returns the resulting image count. A two-pass rename via temp names avoids collisions
    /// (e.g. renaming 10003→10001 while 10001 still exists).
    /// </summary>
    public static int ApplyOrder(string folderPath, IReadOnlyList<string> orderedNames)
    {
        Directory.CreateDirectory(folderPath);

        // Pass 1: move each referenced file (existing number or staged upload) to a neutral temp name.
        var staged = new List<string>();
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var source = Path.Combine(folderPath, Path.GetFileName(orderedNames[i]));
            if (!File.Exists(source))
            {
                continue; // defensively skip a missing reference rather than aborting the whole apply
            }

            var temp = Path.Combine(folderPath, $"__stage_{i}.tmp");
            File.Move(source, temp, overwrite: true);
            staged.Add(temp);
        }

        // Pass 2: delete everything left behind — removed gallery images and unused staged uploads.
        foreach (var file in Directory.EnumerateFiles(folderPath))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("__stage_", StringComparison.Ordinal))
            {
                continue;
            }

            if (GalleryNumber(name) >= FirstGalleryNumber || name.StartsWith(UploadPrefix, StringComparison.Ordinal))
            {
                File.Delete(file);
            }
        }

        // Pass 3: number the staged files contiguously from 10001.
        for (var i = 0; i < staged.Count; i++)
        {
            var final = Path.Combine(folderPath, $"{FirstGalleryNumber + i}.jpg");
            File.Move(staged[i], final, overwrite: true);
        }

        return staged.Count;
    }

    /// <summary>Parses a gallery image number from a file name, or -1 if it is not a numbered image.</summary>
    private static int GalleryNumber(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return int.TryParse(stem, out var number) ? number : -1;
    }
}
