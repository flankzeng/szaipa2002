using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class StaticAssetCachePolicyTests
{
    [Theory]
    [InlineData(StaticAssetSource.WebRoot, "/css/site.css", true)]
    [InlineData(StaticAssetSource.WebRoot, "/css/site.css", false)]
    [InlineData(StaticAssetSource.LegacyContent, "/Content/images/cover.jpg", false)]
    public void DevelopmentAssetsAlwaysBypassCache(
        StaticAssetSource source,
        string path,
        bool hasContentVersion)
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: true,
            source,
            path,
            hasContentVersion);

        Assert.Equal(StaticAssetCachePolicy.NoCache, result);
    }

    [Fact]
    public void VersionedWebRootAssetIsImmutableForOneYear()
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.WebRoot,
            "/css/index.css",
            hasContentVersion: true);

        Assert.Equal(StaticAssetCachePolicy.OneYearImmutable, result);
    }

    [Fact]
    public void UnversionedWebRootAssetKeepsSevenDayCache()
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.WebRoot,
            "/favicon.ico");

        Assert.Equal(StaticAssetCachePolicy.SevenDays, result);
    }

    [Theory]
    [InlineData("/Content/images/cover.jpg")]
    [InlineData("/Content/images/cover.JPEG")]
    [InlineData("/Content/icon/logo.svg")]
    [InlineData("/Content/images/cover.avif")]
    public void LegacyImagesKeepProductionThirtyDayCache(string path)
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.LegacyContent,
            path);

        Assert.Equal(StaticAssetCachePolicy.ThirtyDays, result);
    }

    [Theory]
    [InlineData("/Content/Model/css/Site.css")]
    [InlineData("/Content/Model/swiper-bundle.min.js")]
    public void LegacyStylesAndScriptsUseSevenDayCache(string path)
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.LegacyContent,
            path);

        Assert.Equal(StaticAssetCachePolicy.SevenDays, result);
    }

    [Theory]
    [InlineData("/Content/font/site.woff2")]
    [InlineData("/Content/font/site.ttf")]
    public void LegacyFontsUseThirtyDayCache(string path)
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.LegacyContent,
            path);

        Assert.Equal(StaticAssetCachePolicy.ThirtyDays, result);
    }

    [Fact]
    public void UnknownLegacyAssetKeepsConservativeOneDayCache()
    {
        var result = StaticAssetCachePolicy.Select(
            isDevelopment: false,
            StaticAssetSource.LegacyContent,
            "/Content/data/catalog.json");

        Assert.Equal(StaticAssetCachePolicy.OneDay, result);
    }
}
