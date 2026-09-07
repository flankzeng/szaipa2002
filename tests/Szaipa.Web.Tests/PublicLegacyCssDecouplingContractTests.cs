using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>
/// 2026-08-04 的 CSS 分层合并后，现代公开页不再引用 legacy 的
/// bootstrap.css / Site.css / responsive.css，规则已迁入 wwwroot/css 下的合并文件。
/// 本契约锁定「不回引 legacy」与「公共样式在页面样式之前加载」两点。
/// </summary>
public sealed class PublicLegacyCssDecouplingContractTests
{
    private static readonly string[] LegacyStylesheets =
    [
        "/Content/Model/css/bootstrap.css",
        "/Content/Model/css/Site.css",
        "/Content/Model/css/responsive.css",
        "/Content/Model/css/responsive/Artist.css",
    ];

    [Fact]
    public void Modern_public_views_do_not_reference_legacy_stylesheets()
    {
        var viewsRoot = Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web", "Views");

        foreach (var view in Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(view);
            foreach (var legacy in LegacyStylesheets)
            {
                Assert.DoesNotContain(legacy, markup, StringComparison.OrdinalIgnoreCase);
            }

            // 退役的 Bootstrap 选择开关不应残留，布局已不再读取它。
            Assert.DoesNotContain("UseBootstrapCss", markup, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("_PublicLayout.cshtml")]
    [InlineData("_ArtistLayout.cshtml")]
    public void Public_layouts_load_the_merged_stylesheets_before_page_styles(string layoutName)
    {
        var webProject = Path.Combine(FindRepositoryRoot(), "src", "Szaipa.Web");
        var layout = File.ReadAllText(Path.Combine(webProject, "Views", "Shared", layoutName));

        var expected = new[]
        {
            "~/css/public-core.css",
            "~/css/font-subsets-public.css",
            "~/css/public-layout.css",
        };

        var lastIndex = -1;
        foreach (var href in expected)
        {
            var link = $"<link rel=\"stylesheet\" href=\"{href}\" asp-append-version=\"true\">";
            var index = layout.IndexOf(link, StringComparison.Ordinal);
            Assert.True(index > lastIndex, $"{layoutName} 缺少或顺序错误：{href}");
            lastIndex = index;

            Assert.True(
                File.Exists(Path.Combine(webProject, "wwwroot", "css", Path.GetFileName(href))),
                $"缺少样式文件：{href}");
        }

        var pageStyleIndex = layout.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
        Assert.True(pageStyleIndex > lastIndex, $"{layoutName} 的页面样式段必须在公共样式之后渲染。");
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
