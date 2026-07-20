using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Szaipa.Web.Services;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class DerivedImageResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"szaipa-derived-resolver-{Guid.NewGuid():N}");

    private string WebRoot => Path.Combine(_root, "wwwroot");

    [Fact]
    public void ExistingAllowlistedAvifIsReturnedWithOriginalFallback()
    {
        CreateDerivedFile("home/hero.avif");
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                {
                  "original": "/Content/images/Hero.JPG",
                  "avif": "/media/derived/home/hero.avif?v=abc123"
                }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/content/IMAGES/hero.jpg");

        Assert.NotNull(result);
        Assert.Equal("/content/IMAGES/hero.jpg", result.OriginalUrl);
        Assert.Equal("/media/derived/home/hero.avif?v=abc123", result.AvifUrl);
    }

    [Fact]
    public void ValidUnlistedImageReturnsOnlyOriginalFallback()
    {
        WriteManifest("""{ "schemaVersion": 1, "images": [] }""");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/unlisted.png");

        Assert.NotNull(result);
        Assert.Equal("/Content/images/unlisted.png", result.OriginalUrl);
        Assert.Null(result.AvifUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://www.szaipa.com/Content/images/hero.jpg")]
    [InlineData("//cdn.example.com/Content/images/hero.jpg")]
    [InlineData("Content/images/hero.jpg")]
    [InlineData("/images/hero.jpg")]
    [InlineData("/Content/images/")]
    [InlineData("/Content/images/../secret.jpg")]
    [InlineData("/Content/images/%2e%2e/secret.jpg")]
    [InlineData("/Content/images/%252e%252e/secret.jpg")]
    [InlineData("/Content/images/folder%2fsecret.jpg")]
    [InlineData("/Content/images/folder%252fsecret.jpg")]
    [InlineData("/Content/images/folder\\secret.jpg")]
    [InlineData("/Content/images/hero.avif")]
    [InlineData("/Content/images/hero.webp")]
    [InlineData("/Content/images/hero.svg")]
    public void UnsafeOrUnsupportedOriginalIsRejected(string? original)
    {
        var resolver = CreateResolver();

        Assert.Null(resolver.Resolve(original));
    }

    [Fact]
    public void QueryAndFragmentDoNotChangeManifestLookupAndRemainOnFallback()
    {
        CreateDerivedFile("hero.avif");
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                { "original": "/Content/images/hero.jpg", "avif": "/media/derived/hero.avif" }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg?legacy=1#detail");

        Assert.NotNull(result);
        Assert.Equal("/Content/images/hero.jpg?legacy=1#detail", result.OriginalUrl);
        Assert.Equal("/media/derived/hero.avif", result.AvifUrl);
    }

    [Fact]
    public void MissingManifestSafelyReturnsOriginalFallback()
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Equal("/Content/images/hero.jpg", result.OriginalUrl);
        Assert.Null(result.AvifUrl);
    }

    [Fact]
    public void MalformedManifestSafelyReturnsOriginalFallback()
    {
        WriteManifest("{ not-json");
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void UnsupportedManifestVersionSafelyReturnsOriginalFallback(int version)
    {
        CreateDerivedFile("hero.avif");
        WriteManifest(
            $$"""
            {
              "schemaVersion": {{version}},
              "images": [
                { "original": "/Content/images/hero.jpg", "avif": "/media/derived/hero.avif" }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Fact]
    public void MissingDerivedFileIsNotAdvertised()
    {
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                { "original": "/Content/images/hero.jpg", "avif": "/media/derived/missing.avif" }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Theory]
    [InlineData("https://cdn.example.com/hero.avif")]
    [InlineData("/css/hero.avif")]
    [InlineData("/media/derived/../hero.avif")]
    [InlineData("/media/derived/%2e%2e/hero.avif")]
    [InlineData("/media/derived/hero.jpg")]
    [InlineData("/media/derived/hero.avif?cache=1")]
    [InlineData("/media/derived/hero.avif#fragment")]
    public void InvalidManifestTargetIsIgnored(string avifUrl)
    {
        CreateDerivedFile("hero.avif");
        WriteManifest(
            $$"""
            {
              "schemaVersion": 1,
              "images": [
                { "original": "/Content/images/hero.jpg", "avif": "{{avifUrl}}" }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Fact]
    public void DuplicateCaseInsensitiveOriginalIsTreatedAsAmbiguous()
    {
        CreateDerivedFile("first.avif");
        CreateDerivedFile("second.avif");
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                { "original": "/Content/images/Hero.jpg", "avif": "/media/derived/first.avif" },
                { "original": "/content/IMAGES/hero.JPG", "avif": "/media/derived/second.avif" }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Fact]
    public void EncodedUnicodeAndSpacesMatchDecodedManifestPath()
    {
        CreateDerivedFile("home/exhibition.avif");
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                {
                  "original": "/Content/images/展览 海报.jpg",
                  "avif": "/media/derived/home/exhibition.avif"
                }
              ]
            }
            """);
        var resolver = CreateResolver();
        const string original = "/Content/images/%E5%B1%95%E8%A7%88%20%E6%B5%B7%E6%8A%A5.JPG";

        var result = resolver.Resolve(original);

        Assert.NotNull(result);
        Assert.Equal(original, result.OriginalUrl);
        Assert.Equal("/media/derived/home/exhibition.avif", result.AvifUrl);
    }

    [Fact]
    public void ManifestOriginalWithQueryIsIgnored()
    {
        CreateDerivedFile("hero.avif");
        WriteManifest(
            """
            {
              "schemaVersion": 1,
              "images": [
                {
                  "original": "/Content/images/hero.jpg?v=legacy",
                  "avif": "/media/derived/hero.avif"
                }
              ]
            }
            """);
        var resolver = CreateResolver();

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    [Fact]
    public void BlankWebRootSafelyReturnsOriginalFallback()
    {
        var resolver = new DerivedImageResolver(new TestWebHostEnvironment { WebRootPath = string.Empty });

        var result = resolver.Resolve("/Content/images/hero.jpg");

        Assert.NotNull(result);
        Assert.Null(result.AvifUrl);
    }

    private DerivedImageResolver CreateResolver() =>
        new(new TestWebHostEnvironment
        {
            ContentRootPath = _root,
            WebRootPath = WebRoot,
            ContentRootFileProvider = new PhysicalFileProvider(EnsureDirectory(_root)),
            WebRootFileProvider = new PhysicalFileProvider(EnsureDirectory(WebRoot))
        });

    private void CreateDerivedFile(string relativePath)
    {
        var path = Path.Combine(
            WebRoot,
            "media",
            "derived",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3]);
    }

    private void WriteManifest(string json)
    {
        var path = Path.Combine(WebRoot, "media", "derived", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Szaipa.Web.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
