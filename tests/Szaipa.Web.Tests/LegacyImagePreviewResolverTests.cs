using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class LegacyImagePreviewResolverTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-preview-resolver-{Guid.NewGuid():N}");

    [Fact]
    public void ExistingJpegUsesIndexedPreviewPath()
    {
        CreatePreview("artimg/artist/work.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/ArtImg/Artist/WORK.JPG");

        Assert.Equal("/Content/_preview/q30w1200/Content/artimg/artist/work.jpg", result);
    }

    [Fact]
    public void ConvertedPngUsesJpegPreviewWhenOriginalExtensionIsMissing()
    {
        CreatePreview("newsimg/poster.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/NewsImg/Poster.PNG");

        Assert.Equal("/Content/_preview/q30w1200/Content/newsimg/poster.jpg", result);
    }

    [Fact]
    public void ConvertedJpegTakesPriorityOverFutureOriginalExtensionPreview()
    {
        CreatePreview("newsimg/poster.png");
        CreatePreview("newsimg/poster.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/NewsImg/Poster.PNG");

        Assert.Equal("/Content/_preview/q30w1200/Content/newsimg/poster.jpg", result);
    }

    [Fact]
    public void CaseInsensitiveLookupPreservesActualMixedCasePath()
    {
        CreatePreview("ArtImg/Artist/Works/RealCase.JpG");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/artimg/artist/works/realcase.jpg");

        Assert.Equal("/Content/_preview/q30w1200/Content/ArtImg/Artist/Works/RealCase.JpG", result);
    }

    [Fact]
    public void MissingPreviewReturnsOriginalUrl()
    {
        var resolver = CreateResolver();
        const string original = "/Content/newsimg/missing.jpg";

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void MissingContentRootReturnsOriginalUrl()
    {
        var resolver = CreateResolver(string.Empty);
        const string original = "/Content/newsimg/poster.jpg";

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData("https://www.szaipa.com/Content/newsimg/poster.jpg")]
    [InlineData("//cdn.example.com/Content/newsimg/poster.jpg")]
    [InlineData("/images/poster.jpg")]
    [InlineData("Content/newsimg/poster.jpg")]
    [InlineData("/Content")]
    public void NonContentUrlReturnsOriginalUrl(string original)
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData("/Content/../secret.jpg")]
    [InlineData("/Content/%2e%2e/secret.jpg")]
    [InlineData("/Content/%252e%252e/secret.jpg")]
    [InlineData("/Content/folder/%2E%2E/secret.jpg")]
    [InlineData("/Content/folder%5c..%5csecret.jpg")]
    [InlineData("/Content/folder/%253fsecret.jpg")]
    [InlineData("/Content/folder%2fsecret.jpg")]
    [InlineData("/Content/folder%252fsecret.jpg")]
    [InlineData("/Content/folder\\..\\secret.jpg")]
    [InlineData("/Content//secret.jpg")]
    [InlineData("/Content/_preview/q30w1200/Content/newsimg/poster.jpg")]
    public void UnsafePathReturnsOriginalUrl(string original)
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData("/Content/images/animation.gif")]
    [InlineData("/Content/images/photo.webp")]
    [InlineData("/Content/images/vector.svg")]
    [InlineData("/Content/images/no-extension")]
    public void UnsupportedSourceExtensionReturnsOriginalUrl(string original)
    {
        CreatePreview("images/animation.jpg");
        CreatePreview("images/photo.jpg");
        CreatePreview("images/vector.jpg");
        CreatePreview("images/no-extension.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void AmbiguousCaseInsensitivePreviewNamesReturnOriginalUrl()
    {
        CreatePreview("images/Photo.jpg");
        CreatePreview("images/photo.jpg");
        var previewDirectory = Path.Combine(_contentRoot, "_preview", "q30w1200", "Content", "images");
        if (Directory.GetFiles(previewDirectory).Length < 2)
        {
            return; // The host file system is case-insensitive and cannot represent this ambiguity.
        }

        var resolver = CreateResolver();
        const string original = "/Content/images/PHOTO.jpg";

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void QueryAndFragmentArePreservedOnPreviewUrl()
    {
        CreatePreview("newsimg/poster.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/NewsImg/Poster.jpg?v=42#detail");

        Assert.Equal("/Content/_preview/q30w1200/Content/newsimg/poster.jpg?v=42#detail", result);
    }

    [Fact]
    public void EncodedSpacesAndUnicodeResolveAgainstDecodedIndexedPath()
    {
        CreatePreview("images/展览 海报.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/Images/%E5%B1%95%E8%A7%88%20%E6%B5%B7%E6%8A%A5.JPG");

        Assert.Equal(
            "/Content/_preview/q30w1200/Content/images/%E5%B1%95%E8%A7%88%20%E6%B5%B7%E6%8A%A5.jpg",
            result);
    }

    [Fact]
    public void ApostropheAndMultiDotNameAreEncodedWithoutChangingExtensionSelection()
    {
        CreatePreview("images/artist's poster.final.v2.jpg");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/Images/Artist%27s%20Poster.Final.V2.PNG");

        Assert.Equal(
            "/Content/_preview/q30w1200/Content/images/artist%27s%20poster.final.v2.jpg",
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void BlankInputIsReturnedUnchanged(string original)
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(original);

        Assert.Equal(original, result);
    }

    [Fact]
    public void MissingResultIsCachedForRepeatedResolution()
    {
        var resolver = CreateResolver();
        const string original = "/Content/newsimg/later.jpg";
        Assert.Equal(original, resolver.Resolve(original));

        CreatePreview("newsimg/later.jpg");

        Assert.Equal(original, resolver.Resolve(original));
    }

    private LegacyImagePreviewResolver CreateResolver(string? contentRoot = null) =>
        new(Options.Create(new LegacyAssetsOptions
        {
            ContentRoot = contentRoot ?? _contentRoot
        }));

    private void CreatePreview(string relativePath)
    {
        var physicalPath = Path.Combine(
            _contentRoot,
            "_preview",
            "q30w1200",
            "Content",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllBytes(physicalPath, new byte[] { 1, 2, 3 });
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
