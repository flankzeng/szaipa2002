using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Verifies the exhibition gallery folder renumbering: applying an order produces contiguous 10001.. files
/// in the requested sequence, drops removed images, keeps the cover (10000), and leaves no temp/upload junk.
/// </summary>
public sealed class ExhibitionGalleryFolderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "szaipa-gallery-test-" + Guid.NewGuid().ToString("N"));

    public ExhibitionGalleryFolderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void WriteFile(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    private string ReadFile(string name) => File.ReadAllText(Path.Combine(_dir, name));

    [Fact]
    public void ListGallery_returns_numbered_images_in_order_excluding_cover()
    {
        WriteFile("10000.jpg", "cover");
        WriteFile("10002.jpg", "b");
        WriteFile("10001.jpg", "a");
        WriteFile("notes.txt", "junk");

        Assert.Equal(new[] { "10001.jpg", "10002.jpg" }, ExhibitionGalleryFolder.ListGallery(_dir));
    }

    [Fact]
    public void ApplyOrder_reorders_adds_and_drops_keeping_cover_and_cleaning_up()
    {
        WriteFile("10000.jpg", "cover");
        WriteFile("10001.jpg", "one");
        WriteFile("10002.jpg", "two");   // will be dropped
        WriteFile("10003.jpg", "three");
        WriteFile("__u_new.jpg", "fresh"); // staged upload to insert

        // Desired order: three, fresh, one  (10002 omitted => deleted)
        var count = ExhibitionGalleryFolder.ApplyOrder(_dir, new[] { "10003.jpg", "__u_new.jpg", "10001.jpg" });

        Assert.Equal(3, count);
        Assert.Equal("three", ReadFile("10001.jpg"));
        Assert.Equal("fresh", ReadFile("10002.jpg"));
        Assert.Equal("one", ReadFile("10003.jpg"));
        Assert.Equal("cover", ReadFile("10000.jpg")); // cover untouched

        var files = Directory.EnumerateFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "10000.jpg", "10001.jpg", "10002.jpg", "10003.jpg" }, files);
    }

    [Fact]
    public void ApplyOrder_with_empty_list_clears_gallery_but_keeps_cover()
    {
        WriteFile("10000.jpg", "cover");
        WriteFile("10001.jpg", "one");
        WriteFile("__u_x.jpg", "stray");

        var count = ExhibitionGalleryFolder.ApplyOrder(_dir, Array.Empty<string>());

        Assert.Equal(0, count);
        var files = Directory.EnumerateFiles(_dir).Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { "10000.jpg" }, files);
    }

    [Fact]
    public async Task AddUploadAsync_writes_a_staged_upload_file()
    {
        using var content = new MemoryStream("data"u8.ToArray());
        var name = await ExhibitionGalleryFolder.AddUploadAsync(_dir, content, CancellationToken.None);

        Assert.StartsWith("__u_", name);
        Assert.EndsWith(".jpg", name);
        Assert.True(File.Exists(Path.Combine(_dir, name)));
        Assert.Empty(ExhibitionGalleryFolder.ListGallery(_dir)); // staged uploads are not yet gallery images
    }
}
