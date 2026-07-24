using Szaipa.Web.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class LegacyPublicationGalleryCatalogTests
{
    public static TheoryData<int, int, string, string> MigratedGalleries => new()
    {
        { 92001, 30, "/Content/images/tonggouEurope/100000.jpg", "/Content/images/tonggouEurope/100029.jpg" },
        { 92002, 30, "/Content/images/shuimai/10001.jpg", "/Content/images/shuimai/10034.jpg" },
        { 92003, 12, "/Content/images/chunyu/01.jpg", "/Content/images/chunyu/012.png" },
        { 92005, 24, "/Content/images/zhongyi/10000.jpg", "/Content/images/zhongyi/10023.jpg" },
        { 92006, 23, "/Content/images/tonggou/10001.jpg", "/Content/images/tonggou/10023.jpg" },
        { 92007, 17, "/Content/images/chunyu2/10001.jpg", "/Content/images/chunyu2/100017.jpg" },
        { 92008, 18, "/Content/images/trio/10001.jpg", "/Content/images/trio/100018.jpg" },
        { 92009, 25, "/Content/images/man/10001.jpg", "/Content/images/man/10025.jpg" },
        { 92010, 70, "/Content/images/yijia/10001.jpg", "/Content/images/yijia/10070.jpg" },
        { 92011, 57, "/Content/images/zhongri/10001.jpg", "/Content/images/zhongri/10057.jpg" },
        { 92012, 131, "/Content/images/shuyuyi/100000.jpg", "/Content/images/shuyuyi/100130.jpg" },
        { 92013, 96, "/Content/images/chunyu4/100000.jpg", "/Content/images/chunyu4/100095.jpg" },
        { 92014, 45, "/Content/images/zhongfa/100000.jpg", "/Content/images/zhongfa/100044.jpg" },
        { 92015, 26, "/Content/images/tangqishan/100000.jpg", "/Content/images/tangqishan/100025.jpg" }
    };

    [Theory]
    [MemberData(nameof(MigratedGalleries))]
    public void Migrated_gallery_preserves_exact_legacy_order(
        int publicationId,
        int expectedCount,
        string expectedFirst,
        string expectedLast)
    {
        var images = LegacyPublicationGalleryCatalog.GetImages(publicationId);

        Assert.Equal(expectedCount, images.Count);
        Assert.Equal(expectedFirst, images[0]);
        Assert.Equal(expectedLast, images[^1]);
        Assert.Equal(images.Count, images.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(images, image => Assert.StartsWith("/Content/images/", image, StringComparison.Ordinal));

        var fallback = LegacyPublicationGalleryCatalog.GetFallbackPublication(publicationId);
        Assert.NotNull(fallback);
        Assert.Equal(publicationId, fallback.Publication.Id);
        Assert.Equal(expectedCount - 1, fallback.Publication.MaxImg);
        Assert.Equal(expectedFirst.Split('/')[3], fallback.Publication.FolderName);
        Assert.Equal(0, fallback.Publication.Type);
        Assert.Empty(fallback.RelatedPublications);
    }

    [Fact]
    public void Catalog_excludes_reserved_zengfeng_id_and_unrelated_publications()
    {
        Assert.Empty(LegacyPublicationGalleryCatalog.GetImages(92004));
        Assert.Empty(LegacyPublicationGalleryCatalog.GetImages(1000));
        Assert.Null(LegacyPublicationGalleryCatalog.GetFallbackPublication(92004));
        Assert.Null(LegacyPublicationGalleryCatalog.GetFallbackPublication(1000));
    }

    [Fact]
    public void Zhongfa_catalog_uses_the_existing_typo_filename_instead_of_the_broken_legacy_url()
    {
        var images = LegacyPublicationGalleryCatalog.GetImages(92014);

        Assert.Contains("/Content/images/zhongfa/1000016.jpg", images, StringComparer.Ordinal);
        Assert.DoesNotContain("/Content/images/zhongfa/100016.jpg", images, StringComparer.Ordinal);
    }

    [Fact]
    public void Publication_view_and_both_gallery_skins_use_the_exact_path_catalog()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewsRoot = Path.Combine(repositoryRoot, "src", "Szaipa.Web", "Views");
        var publication = File.ReadAllText(Path.Combine(viewsRoot, "Home", "Publication.cshtml"));
        var gallery = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_ExhibitionGallery.cshtml"));
        var important = File.ReadAllText(Path.Combine(viewsRoot, "Shared", "_ExhibitionImportant.cshtml"));
        var controller = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Szaipa.Web",
            "Controllers",
            "HomeController.cs"));

        Assert.Contains("LegacyPublicationGalleryCatalog.GetImages(publication.Id)", publication, StringComparison.Ordinal);
        Assert.Contains("Model.ImagePaths.Count > 0", gallery, StringComparison.Ordinal);
        Assert.Contains("@foreach (var imagePath in mainImages)", gallery, StringComparison.Ordinal);
        Assert.Contains("@foreach (var imagePath in thumbnailImages)", gallery, StringComparison.Ordinal);
        Assert.Contains("Model.ImagePaths.Count > 0", important, StringComparison.Ordinal);
        Assert.Contains("@foreach (var imagePath in mainImages)", important, StringComparison.Ordinal);
        Assert.Contains("@foreach (var imagePath in thumbnailImages)", important, StringComparison.Ordinal);

        var databaseLookup = controller.IndexOf(
            "GetPublicationDetailSnapshotAsync(id, 0, cancellationToken)",
            StringComparison.Ordinal);
        var fallbackLookup = controller.IndexOf(
            "LegacyPublicationGalleryCatalog.GetFallbackPublication(id)",
            StringComparison.Ordinal);
        Assert.True(databaseLookup >= 0);
        Assert.True(fallbackLookup > databaseLookup);
    }

    [Fact]
    public void Slug_migration_sql_is_transactional_fail_closed_and_inserts_the_catalog_ids()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "sql",
            "2026-07-09-publication-slug-migration.sql"));

        Assert.Contains("SET XACT_ABORT ON;", sql, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK TRANSACTION;", sql, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Publication', N'Type')", sql, StringComparison.Ordinal);
        Assert.Contains("IF EXISTS (SELECT 1 FROM dbo.Publication WHERE Id BETWEEN 92001 AND 92015)", sql, StringComparison.Ordinal);
        Assert.Contains("IF @@ROWCOUNT <> 14", sql, StringComparison.Ordinal);

        var insertedIds = Regex.Matches(sql, @"^\s*\((920\d{2}),", RegexOptions.Multiline)
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();
        var expectedIds = MigratedGalleries.Select(row => (int)row[0]).ToArray();

        Assert.Equal(expectedIds, insertedIds);
        Assert.DoesNotContain(92004, insertedIds);

        foreach (var row in MigratedGalleries)
        {
            var publicationId = (int)row[0];
            var imageCount = (int)row[1];
            var firstImagePath = (string)row[2];
            var folder = firstImagePath.Split('/')[3];
            Assert.Matches(
                $@"\({publicationId},[\s\S]*?N'{Regex.Escape(folder)}',\s*{imageCount - 1},",
                sql);
        }
    }

    [Fact]
    public void Migration_readiness_sql_is_read_only_and_checks_schema_and_reserved_ids()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "sql",
            "2026-07-25-publication-migration-readiness.sql"));

        foreach (var mutation in new[] { "INSERT ", "UPDATE ", "DELETE ", "ALTER ", "CREATE ", "DROP ", "MERGE ", "TRUNCATE " })
        {
            Assert.DoesNotContain(mutation, sql, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("OBJECT_ID(N'dbo.Publication', N'U')", sql, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Publication', required.ColumnName)", sql, StringComparison.Ordinal);
        Assert.Contains("OBJECT_ID(N'dbo.ExhibitionWork', N'U')", sql, StringComparison.Ordinal);
        Assert.Contains("Id BETWEEN 92001 AND 92015", sql, StringComparison.Ordinal);
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
