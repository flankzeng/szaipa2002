using Xunit;

namespace Szaipa.Web.Tests;

public sealed class PublicBootstrapOptOutContractTests
{
    [Fact]
    public void Modern_grid_pages_use_the_versioned_minimal_baseline_instead_of_bootstrap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var viewsRoot = Path.Combine(webProject, "Views");
        var layout = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_newLayout.cshtml"));

        Assert.Contains("ViewData[\"UseBootstrapCss\"] is not false", layout, StringComparison.Ordinal);
        Assert.Contains(
            "<link rel=\"stylesheet\" href=\"~/css/public-bootstrap-baseline.css\" asp-append-version=\"true\">",
            layout,
            StringComparison.Ordinal);
        Assert.Contains(
            "<link rel=\"stylesheet\" href=\"/Content/Model/css/bootstrap.css\">",
            layout,
            StringComparison.Ordinal);

        var layoutStyleIndex = layout.IndexOf("/css/public-layout-main.css", StringComparison.Ordinal);
        var pageStyleIndex = layout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
        Assert.True(layoutStyleIndex >= 0 && pageStyleIndex > layoutStyleIndex);

        foreach (var viewName in new[] { "NewVip.cshtml", "PublicationList.cshtml", "NewNews.cshtml", "NewAbout.cshtml" })
        {
            var view = File.ReadAllText(Path.Combine(viewsRoot, "Home", viewName));
            Assert.Contains("ViewData[\"UseBootstrapCss\"] = false;", view, StringComparison.Ordinal);
        }

        var baselinePath = Path.Combine(webProject, "wwwroot", "css", "public-bootstrap-baseline.css");
        var baseline = File.ReadAllText(baselinePath);
        Assert.Contains("box-sizing: border-box;", baseline, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Helvetica Neue\", Helvetica, Arial, sans-serif;", baseline, StringComparison.Ordinal);
        Assert.Contains("line-height: 1.42857143;", baseline, StringComparison.Ordinal);
        Assert.Contains("figure {", baseline, StringComparison.Ordinal);
        Assert.Contains("h1,\nh2,\nh3,\nh4,\nh5,\nh6 {", baseline, StringComparison.Ordinal);
        Assert.Contains(
            """
            button {
                margin: 0;
                overflow: visible;
                color: inherit;
                font: inherit;
                text-transform: none;
                -webkit-appearance: button;
                cursor: pointer;
            }

            button::-moz-focus-inner {
                padding: 0;
                border: 0;
            }
            """,
            baseline,
            StringComparison.Ordinal);
        Assert.Contains("margin: 0;", baseline, StringComparison.Ordinal);
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
