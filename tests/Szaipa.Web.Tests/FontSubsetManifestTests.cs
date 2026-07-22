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
        var manifestPath = Path.Combine(webRoot, "css", "font-subsets.css");
        var manifest = File.ReadAllText(manifestPath);
        var matches = FontUrlPattern().Matches(manifest);

        Assert.NotEmpty(matches);

        var referencedFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in matches)
        {
            var fileName = match.Groups["file"].Value;
            var version = match.Groups["version"].Value;
            var fontPath = Path.Combine(fontDirectory, fileName);

            Assert.Matches(ContentVersionPattern(), version);
            Assert.True(File.Exists(fontPath), $"Font manifest references a missing file: {fileName}");

            using var stream = File.OpenRead(fontPath);
            var expectedVersion = Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
            Assert.Equal(expectedVersion, version);
            referencedFiles.Add(fileName);
        }

        var shippedFiles = Directory
            .EnumerateFiles(fontDirectory, "*.woff2", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var referencedFilesInOrder = referencedFiles.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(shippedFiles, referencedFilesInOrder);
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
}
