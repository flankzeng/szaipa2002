using Xunit;

namespace Szaipa.Web.Tests;

public sealed class PublicCanonicalRouteContractTests
{
    [Fact]
    public void Public_views_link_only_to_canonical_home_routes()
    {
        var webProject = Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web");
        var publicViews = Directory
            .EnumerateFiles(Path.Combine(webProject, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Staff{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        foreach (var view in publicViews)
        {
            foreach (var legacyPrefix in new[]
                     {
                         "/Home/newIndex",
                         "/Home/newnews",
                         "/Home/newvip",
                         "/Home/newabout",
                         "/Home/newArt",
                         "/Home/ArtNews"
                     })
            {
                Assert.DoesNotContain(legacyPrefix, view, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Constitution_is_a_migrated_public_view_with_rooted_legacy_assets()
    {
        var viewPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Szaipa.Web",
            "Views",
            "Home",
            "Constitution.cshtml");
        var view = File.ReadAllText(viewPath);

        Assert.Contains("Layout = \"~/Views/Shared/_PublicLayout.cshtml\";", view, StringComparison.Ordinal);
        Assert.Contains("深圳艺术产业促进会章程", view, StringComparison.Ordinal);
        Assert.Contains("/Content/images/2021.5.30f2-2.jpg", view, StringComparison.Ordinal);
        Assert.DoesNotContain("../../Content/", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_new_routes_are_redirects_and_canonical_routes_render_canonical_views()
    {
        var controllerPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Szaipa.Web",
            "Controllers",
            "HomeController.cs");
        var controller = File.ReadAllText(controllerPath);

        foreach (var route in new[]
                 {
                     "News",
                     "NewsRead/{id:int}",
                     "Vip",
                     "About",
                     "Constitution",
                     "Art/{id:int}"
                 })
        {
            Assert.Contains($"[HttpGet(\"{route}\")]", controller, StringComparison.Ordinal);
        }

        foreach (var redirect in new[]
                 {
                     "RedirectPermanent(\"/\")",
                     "RedirectPermanent(\"/Home/News\")",
                     "RedirectPermanent($\"/Home/NewsRead/{id}\")",
                     "RedirectPermanent(\"/Home/Vip\")",
                     "RedirectPermanent(\"/Home/About\")",
                     "RedirectPermanent(\"/Home/Constitution\")",
                     "RedirectPermanent($\"/Home/Art/{id}\")"
                 })
        {
            Assert.Contains(redirect, controller, StringComparison.Ordinal);
        }

        Assert.Contains("[HttpGet(\"link\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"newlink\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"Art/{id:int}/Archive\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"Art/News/{id:int}\")]", controller, StringComparison.Ordinal);
        Assert.Contains("GetArtistArchiveAsync", controller, StringComparison.Ordinal);
        Assert.Contains("GetArtistArticleAsync", controller, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Szaipa.Web")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Szaipa repository root.");
    }
}
