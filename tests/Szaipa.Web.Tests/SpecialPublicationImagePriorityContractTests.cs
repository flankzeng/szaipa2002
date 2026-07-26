using Xunit;

namespace Szaipa.Web.Tests;

public sealed class SpecialPublicationImagePriorityContractTests
{
    [Fact]
    public void Below_fold_special_publication_brand_images_do_not_compete_with_the_hero()
    {
        var viewsRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Szaipa.Web",
            "Views",
            "Publication");

        foreach (var viewName in new[] { "chunyu3.cshtml", "tonggou2.cshtml", "tonggou2024.cshtml" })
        {
            var view = File.ReadAllText(Path.Combine(viewsRoot, viewName));

            Assert.Contains("class=\"tonggou\"", view, StringComparison.Ordinal);
            Assert.Contains("class=\"elementPub\"", view, StringComparison.Ordinal);
            Assert.Equal(2, Count(view, "loading=\"lazy\" decoding=\"async\" fetchpriority=\"low\""));
            Assert.DoesNotContain("fetchpriority=\"high\"", view, StringComparison.Ordinal);
        }
    }

    private static int Count(string value, string target)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(target, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += target.Length;
        }

        return count;
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
