using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services.Admin;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class AdminAssetStorageTests
{
    [Fact]
    public async Task Saves_original_beside_crop_and_can_clone_it_for_recrop()
    {
        var root = Path.Combine(Path.GetTempPath(), "szaipa-assets-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new AdminAssetStorage(Options.Create(new LegacyAssetsOptions { ContentRoot = root }));
            var original = new FormFile(new MemoryStream([1, 2, 3, 4]), 0, 4, "original", "source.png");
            var cropped = new FormFile(new MemoryStream([9, 8]), 0, 2, "file", "crop.jpg");
            var saved = await storage.SaveImageAsync(cropped, "ArtImg/Artist", CancellationToken.None);
            Assert.True(saved.Success);

            var originalUrl = await storage.SaveOriginalAsync(original, "ArtImg/Artist", saved.FileName!, CancellationToken.None);
            Assert.NotNull(originalUrl);
            Assert.Equal(originalUrl, storage.FindOriginalUrl("ArtImg/Artist", saved.FileName!));

            var cloneUrl = await storage.CloneOriginalAsync("ArtImg/Artist", saved.FileName!, "recrop.jpg", CancellationToken.None);
            Assert.NotNull(cloneUrl);
            Assert.Equal(cloneUrl, storage.FindOriginalUrl("ArtImg/Artist", "recrop.jpg"));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(root, "ArtImg", "Artist", "original", "recrop.png")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
