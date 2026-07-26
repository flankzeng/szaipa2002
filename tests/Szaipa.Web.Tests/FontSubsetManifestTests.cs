using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed partial class FontSubsetManifestTests
{
    [Fact]
    public void Every_shipped_font_has_a_matching_content_version()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webRoot = Path.Combine(repositoryRoot, "src", "Szaipa.Web", "wwwroot");
        var fontDirectory = Path.Combine(webRoot, "fonts");
        var referencedFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var manifestName in new[] { "font-subsets-public.css", "font-subsets-staff.css" })
        {
            var manifestPath = Path.Combine(webRoot, "css", manifestName);
            var manifest = File.ReadAllText(manifestPath);
            var matches = FontUrlPattern().Matches(manifest);

            Assert.NotEmpty(matches);
            foreach (Match match in matches)
            {
                var fileName = match.Groups["file"].Value;
                var version = match.Groups["version"].Value;
                var fontPath = Path.Combine(fontDirectory, fileName);

                Assert.Matches(ContentVersionPattern(), version);
                Assert.True(File.Exists(fontPath), $"{manifestName} references a missing file: {fileName}");

                using var stream = File.OpenRead(fontPath);
                var expectedVersion = Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
                Assert.Equal(expectedVersion, version);
                referencedFiles.Add(fileName);
            }
        }

        var shippedFiles = Directory
            .EnumerateFiles(fontDirectory, "*.woff2", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var referencedFilesInOrder = referencedFiles.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(shippedFiles, referencedFilesInOrder);
    }

    [Fact]
    public void Public_and_staff_manifests_keep_their_exact_font_boundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var cssDirectory = Path.Combine(webProject, "wwwroot", "css");
        var publicManifest = File.ReadAllText(Path.Combine(cssDirectory, "font-subsets-public.css"));
        var staffManifest = File.ReadAllText(Path.Combine(cssDirectory, "font-subsets-staff.css"));
        var publicFaces = FontFacePattern().Matches(publicManifest).Cast<Match>().Select(match => match.Value.Trim()).ToHashSet(StringComparer.Ordinal);
        var staffFaces = FontFacePattern().Matches(staffManifest).Cast<Match>().Select(match => match.Value.Trim()).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(114, publicFaces.Count);
        Assert.Equal(88, staffFaces.Count);
        Assert.Equal(147, publicFaces.Union(staffFaces).Count());
        Assert.Equal(55, publicFaces.Intersect(staffFaces).Count());

        Assert.Contains("Szaipa Noto Sans SC", publicManifest, StringComparison.Ordinal);
        Assert.Contains("Szaipa Noto Serif SC", publicManifest, StringComparison.Ordinal);
        Assert.Contains("Alibaba PuHuiTi Local", publicManifest, StringComparison.Ordinal);
        Assert.Contains("smile-core", publicManifest, StringComparison.Ordinal);
        Assert.Contains(".notosans", publicManifest, StringComparison.Ordinal);
        Assert.Contains(".notoserif", publicManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("Szaipa Noto Serif SC Staff", publicManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("font-weight: 600;", publicManifest, StringComparison.Ordinal);

        Assert.Contains("Szaipa Noto Sans SC", staffManifest, StringComparison.Ordinal);
        Assert.Contains("Szaipa Noto Serif SC Staff", staffManifest, StringComparison.Ordinal);
        Assert.Contains("font-weight: 400;", staffManifest, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600;", staffManifest, StringComparison.Ordinal);
        Assert.Contains("font-weight: 700;", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("font-family: \"Szaipa Noto Serif SC\";", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("font-family: \"Szaipa Noto Serif SC GB\";", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("Alibaba PuHuiTi Local", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("smile-core", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain(".Jheng", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain(".notosans", staffManifest, StringComparison.Ordinal);
        Assert.DoesNotContain(".notoserif", staffManifest, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(cssDirectory, "font-subsets.css")));
    }

    [Fact]
    public void Razor_surfaces_load_only_their_scoped_font_manifest()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProject = Path.Combine(repositoryRoot, "src", "Szaipa.Web");
        var publicViews = new[]
        {
            "Views/Shared/_PublicLayout.cshtml",
            "Views/Shared/_ArtistLayout.cshtml",
            "Views/Home/NewsRead.cshtml",
            "Views/Publication/chunyu3.cshtml",
            "Views/Publication/tonggou2.cshtml",
            "Views/Publication/tonggou2024.cshtml",
        };

        foreach (var relativePath in publicViews)
        {
            var view = File.ReadAllText(Path.Combine(webProject, relativePath));
            Assert.Contains("font-subsets-public.css", view, StringComparison.Ordinal);
            Assert.DoesNotContain("font-subsets-staff.css", view, StringComparison.Ordinal);
        }

        var staffLayout = File.ReadAllText(Path.Combine(webProject, "Areas", "Staff", "Views", "Shared", "_StaffLayout.cshtml"));
        Assert.Contains("font-subsets-staff.css", staffLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("font-subsets-public.css", staffLayout, StringComparison.Ordinal);

        var allViews = Directory
            .EnumerateFiles(webProject, "*.cshtml", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        Assert.DoesNotContain(allViews, view => view.Contains("/css/font-subsets.css\"", StringComparison.Ordinal));
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

    [GeneratedRegex("url\\([\\\"']?\\.\\./fonts/(?<file>[^?\\\"')]+\\.woff2)(?:\\?v=(?<version>[^\\\"')]+))?[\\\"']?\\)")]
    private static partial Regex FontUrlPattern();

    [GeneratedRegex("^[0-9a-f]{12}$")]
    private static partial Regex ContentVersionPattern();

    [GeneratedRegex("@font-face\\s*\\{.*?\\}", RegexOptions.Singleline)]
    private static partial Regex FontFacePattern();
}
