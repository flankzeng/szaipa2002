using Szaipa.Web.Services;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class ArtistLinksTests
{
    [Fact]
    public void Blank_or_unlinked_artists_do_not_navigate_and_unknown_data_keeps_links()
    {
        IReadOnlySet<int> withHomepage = new HashSet<int> { 1000 };

        Assert.Equal("/Home/Art/1000", ArtistLinks.Href(1000, withHomepage));
        Assert.Equal("/Home/Art/1000#section02", ArtistLinks.Href(1000, withHomepage, "#section02"));
        Assert.Equal("javascript:;;", ArtistLinks.Href(1052, withHomepage));
        Assert.Equal("javascript:;;", ArtistLinks.Href(null, withHomepage));
        Assert.Equal("/Home/Art/1052", ArtistLinks.Href(1052, null)); // 读库未启用：不误伤
        Assert.False(ArtistLinks.HasHomepage(1052, withHomepage));
    }
}
