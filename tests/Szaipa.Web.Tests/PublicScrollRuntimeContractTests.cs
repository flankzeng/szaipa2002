using Xunit;

namespace Szaipa.Web.Tests;

public sealed class PublicScrollRuntimeContractTests
{
    [Fact]
    public void Repeated_scroll_behaviors_are_versioned_external_assets()
    {
        var webProject = Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web");
        var viewsRoot = Path.Combine(webProject, "Views");

        var layout = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_PublicLayout.cshtml"));
        Assert.Contains(
            "<script src=\"~/js/public-layout-main.js\" asp-append-version=\"true\" defer></script>",
            layout,
            StringComparison.Ordinal);
        Assert.DoesNotContain("window.requestAnimationFrame", layout, StringComparison.Ordinal);

        var newsDetail = File.ReadAllText(Path.Combine(viewsRoot, "Home", "NewsRead.cshtml"));
        Assert.Contains(
            "<script src=\"~/js/newsread.js\" asp-append-version=\"true\" defer></script>",
            newsDetail,
            StringComparison.Ordinal);
        Assert.DoesNotContain("const toTopButton", newsDetail, StringComparison.Ordinal);

        var layoutScript = File.ReadAllText(Path.Combine(webProject, "wwwroot", "js", "public-layout-main.js"));
        Assert.Contains("window.requestAnimationFrame", layoutScript, StringComparison.Ordinal);
        Assert.Contains("{ passive: true }", layoutScript, StringComparison.Ordinal);

        var newsScript = File.ReadAllText(Path.Combine(webProject, "wwwroot", "js", "newsread.js"));
        Assert.Contains("window.scrollY >= 500", newsScript, StringComparison.Ordinal);
        Assert.Contains("behavior: 'smooth'", newsScript, StringComparison.Ordinal);
        Assert.Contains("{ passive: true }", newsScript, StringComparison.Ordinal);
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
