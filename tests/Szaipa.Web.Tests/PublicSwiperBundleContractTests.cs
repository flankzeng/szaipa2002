using System.Text.Json;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class PublicSwiperBundleContractTests
{
    [Fact]
    public void Public_pages_use_the_versioned_module_bundle_instead_of_the_legacy_full_bundle()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var sourceRoot = Path.Combine(webRoot, "wwwroot", "public", "src");
        var vendorRoot = Path.Combine(webRoot, "wwwroot", "public", "vendor");

        var javascriptSource = File.ReadAllText(Path.Combine(sourceRoot, "swiper-public.js"));
        foreach (var module in new[]
                 {
                     "A11y",
                     "Autoplay",
                     "EffectCoverflow",
                     "FreeMode",
                     "Keyboard",
                     "Navigation",
                     "Pagination",
                     "Thumbs"
                 })
        {
            Assert.Contains(module, javascriptSource, StringComparison.Ordinal);
        }

        Assert.Contains("window.Swiper = Swiper", javascriptSource, StringComparison.Ordinal);

        var javascriptBundle = new FileInfo(Path.Combine(vendorRoot, "swiper-public.js"));
        var cssBundle = new FileInfo(Path.Combine(vendorRoot, "swiper-public.css"));
        Assert.True(javascriptBundle.Exists);
        Assert.True(cssBundle.Exists);
        Assert.InRange(javascriptBundle.Length, 1, 100_000);
        Assert.InRange(cssBundle.Length, 1, 15_000);

        using var packageDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(webRoot, "package.json")));
        Assert.Equal(
            "9.0.3",
            packageDocument.RootElement.GetProperty("dependencies").GetProperty("swiper").GetString());
        Assert.Contains(
            "build:public-swiper",
            packageDocument.RootElement.GetProperty("scripts").GetProperty("build").GetString(),
            StringComparison.Ordinal);

        var razorFiles = new[]
        {
            Path.Combine("Views", "Shared", "_Artist.cshtml"),
            Path.Combine("Views", "Home", "NewArt.cshtml"),
            Path.Combine("Views", "Home", "NewIndex.cshtml"),
            Path.Combine("Views", "Home", "Publication.cshtml"),
            Path.Combine("Views", "Publication", "chunyu3.cshtml"),
            Path.Combine("Views", "Publication", "tonggou2.cshtml"),
            Path.Combine("Views", "Publication", "tonggou2024.cshtml")
        };

        foreach (var relativePath in razorFiles)
        {
            var razor = File.ReadAllText(Path.Combine(webRoot, relativePath));
            Assert.DoesNotContain("/Content/Model/swiper-bundle", razor, StringComparison.Ordinal);
            Assert.Contains("~/public/vendor/swiper-public.", razor, StringComparison.Ordinal);
            Assert.Contains("asp-append-version=\"true\"", razor, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "src", "Szaipa.Web")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Szaipa repository root.");
    }
}
