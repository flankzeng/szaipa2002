using Xunit;

namespace Szaipa.Web.Tests;

public sealed class PublicBootstrapOptOutContractTests
{
    [Fact]
    public void Modern_public_pages_use_the_versioned_minimal_baseline_instead_of_bootstrap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var viewsRoot = Path.Combine(webProject, "Views");
        var layout = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_newLayout.cshtml"));
        var artistLayout = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_Artist.cshtml"));

        foreach (var publicLayout in new[] { layout, artistLayout })
        {
            Assert.Contains("ViewData[\"UseBootstrapCss\"] is not false", publicLayout, StringComparison.Ordinal);
            Assert.Contains(
                "<link rel=\"stylesheet\" href=\"~/css/public-bootstrap-baseline.css\" asp-append-version=\"true\">",
                publicLayout,
                StringComparison.Ordinal);
            Assert.Contains(
                "<link rel=\"stylesheet\" href=\"/Content/Model/css/bootstrap.css\">",
                publicLayout,
                StringComparison.Ordinal);
        }

        var layoutStyleIndex = layout.IndexOf("/css/public-layout-main.css", StringComparison.Ordinal);
        var pageStyleIndex = layout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
        Assert.True(layoutStyleIndex >= 0 && pageStyleIndex > layoutStyleIndex);

        var artistLayoutStyleIndex = artistLayout.IndexOf("/css/public-layout-artist.css", StringComparison.Ordinal);
        var artistPageStyleIndex = artistLayout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
        Assert.True(artistLayoutStyleIndex >= 0 && artistPageStyleIndex > artistLayoutStyleIndex);

        foreach (var viewName in new[] { "NewIndex.cshtml", "NewVip.cshtml", "PublicationList.cshtml", "NewNews.cshtml", "NewAbout.cshtml" })
        {
            var view = File.ReadAllText(Path.Combine(viewsRoot, "Home", viewName));
            Assert.Contains("ViewData[\"UseBootstrapCss\"] = false;", view, StringComparison.Ordinal);
        }

        var newArt = File.ReadAllText(Path.Combine(viewsRoot, "Home", "NewArt.cshtml"));
        Assert.Contains("ViewData[\"UseBootstrapCss\"] = false;", newArt, StringComparison.Ordinal);

        var publication = File.ReadAllText(Path.Combine(viewsRoot, "Home", "Publication.cshtml"));
        Assert.Contains("ViewData[\"UseBootstrapCss\"] = false;", publication, StringComparison.Ordinal);

        foreach (var viewName in new[] { "chunyu3.cshtml", "tonggou2.cshtml", "tonggou2024.cshtml" })
        {
            var specialPublication = File.ReadAllText(Path.Combine(viewsRoot, "Publication", viewName));
            Assert.Contains(
                "<link rel=\"stylesheet\" href=\"~/css/public-bootstrap-baseline.css\" asp-append-version=\"true\">",
                specialPublication,
                StringComparison.Ordinal);
            Assert.DoesNotContain("/Content/Model/css/bootstrap.css", specialPublication, StringComparison.Ordinal);
        }

        var baselinePath = Path.Combine(webProject, "wwwroot", "css", "public-bootstrap-baseline.css");
        var baseline = File.ReadAllText(baselinePath);
        Assert.Contains("box-sizing: border-box;", baseline, StringComparison.Ordinal);
        Assert.Contains(
            """
            .clearfix:before,
            .clearfix:after {
                display: table;
                content: " ";
            }

            .clearfix:after {
                clear: both;
            }
            """,
            baseline,
            StringComparison.Ordinal);
        Assert.Contains("font-family: \"Helvetica Neue\", Helvetica, Arial, sans-serif;", baseline, StringComparison.Ordinal);
        Assert.Contains("line-height: 1.42857143;", baseline, StringComparison.Ordinal);
        Assert.Contains("figure {", baseline, StringComparison.Ordinal);
        Assert.Contains("h1,\nh2,\nh3,\nh4,\nh5,\nh6 {", baseline, StringComparison.Ordinal);
        Assert.Contains("h1 {\n    font-size: 36px;\n}", baseline, StringComparison.Ordinal);
        Assert.Contains(
            """
            button,
            input,
            optgroup,
            select,
            textarea {
                margin: 0;
                color: inherit;
                font: inherit;
            }

            button {
                overflow: visible;
            }

            button,
            select {
                text-transform: none;
            }

            button,
            html input[type="button"],
            input[type="reset"],
            input[type="submit"] {
                -webkit-appearance: button;
                cursor: pointer;
            }

            button[disabled],
            html input[disabled] {
                cursor: default;
            }

            button::-moz-focus-inner,
            input::-moz-focus-inner {
                padding: 0;
                border: 0;
            }
            """,
            baseline,
            StringComparison.Ordinal);
        Assert.Contains(
            """
            table {
                border-spacing: 0;
                border-collapse: collapse;
                background-color: transparent;
            }

            td,
            th {
                padding: 0;
            }

            th {
                text-align: left;
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
