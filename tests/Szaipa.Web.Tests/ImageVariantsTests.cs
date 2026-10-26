using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Szaipa.Web.Configuration;
using Szaipa.Web.Services;
using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>T8 图片变体：按需生成 WebP、不放大、路径安全、srcset 与富文本改写、关闭时回退原图。</summary>
public sealed class ImageVariantsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "szaipa-variants-" + Guid.NewGuid().ToString("N"));
    private readonly string _content;
    private readonly string _cache;

    public ImageVariantsTests()
    {
        _content = Path.Combine(_root, "Content");
        _cache = Path.Combine(_root, "cache");
        Directory.CreateDirectory(Path.Combine(_content, "images", "show"));
        WriteJpeg(Path.Combine(_content, "images", "show", "big photo.jpg"), 3000, 2000);
        WriteJpeg(Path.Combine(_content, "images", "small.jpg"), 300, 200);
    }

    [Fact]
    public async Task Generates_a_smaller_webp_at_the_requested_width_and_reuses_the_cache()
    {
        var variants = Create();
        var url = variants.Url("/Content/images/show/big%20photo.jpg", 960);
        Assert.StartsWith("/_img/960/Content/images/show/big%20photo.jpg?v=", url);

        var file = await variants.GetOrCreateAsync(960, "images/show/big photo.jpg", default);
        Assert.NotNull(file);
        Assert.StartsWith(_cache, file);
        using (var codec = SKCodec.Create(file))
        {
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.Equal(960, codec.Info.Width);
            Assert.Equal(640, codec.Info.Height);
        }

        Assert.True(new FileInfo(file!).Length < new FileInfo(Path.Combine(_content, "images", "show", "big photo.jpg")).Length);
        var written = File.GetLastWriteTimeUtc(file!);
        Assert.Equal(file, await variants.GetOrCreateAsync(960, "images/show/big photo.jpg", default));
        Assert.Equal(written, File.GetLastWriteTimeUtc(file!)); // 第二次直接用缓存
    }

    [Fact]
    public async Task Never_upscales_small_images()
    {
        var file = await Create().GetOrCreateAsync(2400, "images/small.jpg", default);
        using var codec = SKCodec.Create(file);
        Assert.Equal(300, codec.Info.Width);
    }

    [Theory]
    [InlineData("/Content/../appsettings.json")]
    [InlineData("/Content/images/%2e%2e/%2e%2e/secret.jpg")]
    [InlineData("/Content/images/missing.jpg")]
    [InlineData("/Content/images/readme.txt")]
    [InlineData("/Other/images/small.jpg")]
    [InlineData("https://evil.example.com/Content/a.jpg")]
    public void Ineligible_or_unsafe_urls_are_returned_unchanged(string url)
    {
        Assert.Equal(url, Create().Url(url, 960));
    }

    [Fact]
    public async Task Unsafe_relative_paths_and_unknown_widths_are_refused()
    {
        var variants = Create();
        Assert.Null(await variants.GetOrCreateAsync(960, "../Content/images/small.jpg", default));
        Assert.Null(await variants.GetOrCreateAsync(961, "images/small.jpg", default));
    }

    [Fact]
    public void SrcSet_skips_small_files_and_previews_and_html_images_get_srcset()
    {
        var variants = Create();
        Assert.Null(variants.SrcSet("/Content/images/small.jpg", minBytes: 1_000_000));
        Assert.Null(variants.SrcSet("/Content/_preview/q30w1200/Content/images/small.jpg"));
        var srcset = variants.SrcSet("/Content/images/show/big%20photo.jpg", minBytes: 0);
        Assert.NotNull(srcset);
        Assert.Contains("/_img/480/", srcset);
        Assert.Contains(" 2400w", srcset);

        var html = variants.AddSrcSetToHtml("<p><img src=\"/Content/images/show/big%20photo.jpg\" alt=\"\"><img src=\"https://x.example/a.jpg\"></p>", "100vw");
        Assert.Contains("srcset=\"/_img/480/", html);
        Assert.Contains("sizes=\"100vw\"", html);
        Assert.Contains("<img src=\"https://x.example/a.jpg\">", html); // 外链不动
    }

    [Fact]
    public void Disabled_or_unconfigured_production_falls_back_to_originals()
    {
        Assert.Equal("/Content/images/small.jpg", Create(enabled: false).Url("/Content/images/small.jpg", 960));
        var production = new ImageVariants(
            Options.Create(new LegacyAssetsOptions { ContentRoot = _content }),
            Options.Create(new ImageVariantOptions()),
            new FakeEnvironment("Production", _root),
            NullLogger<ImageVariants>.Instance);
        Assert.False(production.Enabled);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ImageVariants Create(bool enabled = true) => new(
        Options.Create(new LegacyAssetsOptions { ContentRoot = _content }),
        Options.Create(new ImageVariantOptions { Enabled = enabled, CacheRoot = _cache }),
        new FakeEnvironment("Development", _root),
        NullLogger<ImageVariants>.Instance);

    private static void WriteJpeg(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), new[] { SKColors.DarkRed, SKColors.LightGray }, SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private sealed class FakeEnvironment(string name, string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Szaipa.Web";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
