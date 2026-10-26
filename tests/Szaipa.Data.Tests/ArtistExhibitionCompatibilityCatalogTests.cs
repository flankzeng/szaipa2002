using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

public sealed class ArtistExhibitionCompatibilityCatalogTests
{
    [Fact]
    public void The_three_legacy_exhibition_slots_remain_selectable_when_publication_has_no_rows()
    {
        var options = ArtistExhibitionCompatibilityCatalog.GetOptions();
        Assert.Equal(15, options.Count);
        Assert.Contains(options, option => option.Id == ArtistExhibitionCompatibilityCatalog.SpringOneId);
        Assert.Contains(options, option => option.Id == ArtistExhibitionCompatibilityCatalog.SpringTwoId);
        Assert.Contains(options, option => option.Id == ArtistExhibitionCompatibilityCatalog.TonggouTwoId);
        Assert.Contains(options, option => option.Id == 92015);
    }
}
