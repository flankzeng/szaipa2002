using Szaipa.Web.Infrastructure;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class StaticAssetContentTypesTests
{
    [Fact]
    public void AvifUsesExplicitImageMimeType()
    {
        var provider = StaticAssetContentTypes.CreateProvider();

        var found = provider.TryGetContentType("hero.avif", out var contentType);

        Assert.True(found);
        Assert.Equal("image/avif", contentType);
    }
}
