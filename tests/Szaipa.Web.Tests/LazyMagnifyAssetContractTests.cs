using Xunit;

namespace Szaipa.Web.Tests;

public sealed class LazyMagnifyAssetContractTests
{
    [Fact]
    public void Public_views_only_reference_magnify_through_the_versioned_lazy_loader()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var viewsRoot = Path.Combine(webProject, "Views");
        var publicViews = string.Join(
            "\n",
            Directory
                .EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("jquery-3.6.1.min.js", publicViews, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jquery.magnify.js", publicViews, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("css/magnify.css", publicViews, StringComparison.OrdinalIgnoreCase);

        foreach (var viewName in new[] { "NewIndex.cshtml", "NewArt.cshtml" })
        {
            var view = File.ReadAllText(Path.Combine(viewsRoot, "Home", viewName));
            Assert.Contains(
                "<script src=\"~/js/magnify-loader.js\" asp-append-version=\"true\"></script>",
                view,
                StringComparison.Ordinal);
        }

        Assert.True(
            File.Exists(Path.Combine(webProject, "wwwroot", "js", "magnify-loader.js")),
            "The versioned view reference must point to the shared lazy loader.");

        foreach (var styleName in new[] { "newindex.css", "newart.css" })
        {
            var pageStyle = File.ReadAllText(Path.Combine(webProject, "wwwroot", "css", styleName));
            Assert.Contains("body .magnify {", pageStyle, StringComparison.Ordinal);
            Assert.Contains("body .magnify > .magnify-lens {", pageStyle, StringComparison.Ordinal);
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
